using System;
using System.Net;
using ExtM3UPlaylistParser.Models;
using ExtM3UPlaylistParser.Parsers;
using ExtM3UPlaylistParser.Playlists;
using ExtM3UPlaylistParser.Tags.Master;
using TwitchStreamDownloader.Exceptions;
using TwitchStreamDownloader.Net;
using TwitchStreamDownloader.Resources;

namespace TwitchStreamDownloader.Download
{
    public class LineEventArgs : EventArgs
    {
        public bool Master { get; set; }
        public string Line { get; set; }

        public LineEventArgs(bool master, string line)
        {
            Master = master;
            Line = line;
        }
    }

    /// <summary>
    /// Эта залупа просто качает плейлист и выдаёт сегменты в порядке очереди.
    /// Сегмент в данном случае это инфа о сегменте видео, а не сами видео
    /// </summary>
    public class SegmentsDownloader : IDisposable
    {
        public bool Disposed { get; private set; } = false;

        /// <summary>
        /// програмдейт последнего сегмента
        /// </summary>
        public DateTimeOffset? LastMediaTime { get; private set; } = null;
        public int LastMediaSequenceNumber { get; private set; } = -1;

        public MasterPlaylistPrint? LastMasterPlaylistPrint { get; private set; }
        /// <summary>
        /// UTC
        /// </summary>
        public DateTime? LastMediaPlaylistUpdate { get; private set; } = null;

        public string? LastVideo { get; private set; } = null;

        //Предполагалось, что сюда можно будет руками из кеша положить, но мне стало впадлу.
        public AccessToken? Access { get; private set; }
        public string DeviceId { get; private set; }
        public string SessionId { get; private set; }

        public int TokenAcquiranceFailedAttempts { get; private set; } = 0;

        public readonly HttpClient httpClient;
        //Вообще, раз уж качамба отдельно идёт, нужно бы вынести её. Но да ладно
        //по факту это отмена всего качатора. можно было бы второй токен связать с этим, но там диспозед есть, так что похуй?
        private readonly CancellationTokenSource cancellationTokenSourceWeb;
        private CancellationTokenSource cancellationTokenSourceLoop;

        private readonly Random random = new();

        private readonly SegmentsDownloaderSettings settings;
        private readonly string channel;
        private string? clientId;
        private string? oauth;

        public event EventHandler? PlaylistEnded;
        public event EventHandler<Exception>? MasterPlaylistExceptionOccured;
        public event EventHandler<Exception>? MediaPlaylistExceptionOccured;
        /// <summary>
        /// LastVideo содержит предыдущее качество
        /// </summary>
        public event EventHandler<VariantStream>? MediaQualitySelected;
        /// <summary>
        /// Сюда прибывают сегменты
        /// </summary>
        public event EventHandler<StreamSegment>? SegmentArrived;
        public event EventHandler? MediaPlaylistProcessed;
        public event EventHandler<LineEventArgs>? UnknownPlaylistLineFound;
        public event EventHandler<LineEventArgs>? CommentPlaylistLineFound;

        /// <summary>
        /// Успешно получили токен
        /// </summary>
        public event EventHandler<AccessToken>? TokenAcquired;

        /// <summary>
        /// Выдало ошибку, когда обновлялся токен сам
        /// </summary>
        public event EventHandler<Exception>? TokenAcquiringExceptionOccured;

        //дыбажим
        public event Action<MasterPlaylist>? MasterPlaylistDebugHandler;
        public event Action<MediaPlaylist>? MediaPlaylistDebugHandler;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="httpClient">Не забудь его задиспоузить сам.</param>
        /// <param name="settings"></param>
        /// <param name="channel"></param>
        /// <param name="clientId"></param>
        /// <param name="oauth"></param>
        public SegmentsDownloader(HttpClient httpClient, SegmentsDownloaderSettings settings, string channel, string? clientId, string? oauth)
        {
            this.settings = settings;
            this.channel = channel;
            this.clientId = clientId;
            this.oauth = oauth;

            this.httpClient = httpClient;

            cancellationTokenSourceWeb = new CancellationTokenSource();
            cancellationTokenSourceLoop = new CancellationTokenSource();

            DeviceId = GqlNet.GenerateDeviceId(random);
            SessionId = UsherNet.GenerateUniqueId(random);
        }

        public void SetCreds(string? clientId, string? oauth)
        {
            this.clientId = clientId;
            this.oauth = oauth;
        }

        /// <exception cref="BadCodeException">Если хттп код не саксес.</exception>
        /// <exception cref="WrongContentException">Если содержимое ответа не такое, какое хотелось бы.</exception>
        /// <exception cref="TaskCanceledException">Отменили.</exception>
        /// <exception cref="Exception">Скорее всего, не удалось совершить запрос.</exception>
        public async Task<AccessToken> RequestToken()
        {
            const string undefined = "undefined";

            string requestClientId = clientId ?? "kimne78kx3ncx6brgo4mv6wki5h1ko";
            string requestOauth = oauth ?? undefined;

            return await GqlNet.GetAccessToken(httpClient, channel, requestClientId, DeviceId, requestOauth, cancellationTokenSourceLoop.Token);
        }

        /// <summary>
        /// Изо всех сил пытается достать токен. Ошибки не бросает. Ну, хендлеры могут в тевории.
        /// </summary>
        /// <returns>нулл, если хуйня отменилась</returns>
        async Task<AccessToken?> RequestTokenToTheLimit(CancellationToken cancellationToken)
        {
            /* Вообще, счётчик попыток стоило бы сделать локальной хуйнёй?
             * А выдавать через аргументы события.
             * Но дезинг вроде такой, что не может быть два ретрая онлайн за раз.
             * Так что похуй, надеюсь.
             * Но тогда не нужно в начале обнулять, если ошибок нет :) */
            TokenAcquiranceFailedAttempts = 0;
            AccessToken? token = null;

            while (token == null && !cancellationToken.IsCancellationRequested && !Disposed)
            {
                try
                {
                    token = await RequestToken();
                }
                catch (Exception e)
                {
                    TokenAcquiranceFailedAttempts++;

                    TokenAcquiringExceptionOccured?.Invoke(this, e);

                    bool shortDelay;
                    if (oauth != null && settings.oauthTokenFailedAttemptsLimit != -1 && TokenAcquiranceFailedAttempts >= settings.oauthTokenFailedAttemptsLimit)
                    {
                        SetCreds(null, null);

                        shortDelay = true;
                    }
                    else
                    {
                        shortDelay = TokenAcquiranceFailedAttempts == 1;
                    }

                    TimeSpan delay = shortDelay ? settings.shortAccessTokenRetryDelay : settings.accessTokenRetryDelay;
                    try { await Task.Delay(delay, cancellationToken); } catch { break; }
                }
            }

            //Тупо, что 2 проверки. Но они дешёвые, а алгоритм получше ещё придумать нужно.
            if (token == null || cancellationToken.IsCancellationRequested || Disposed)
            {
                TokenAcquiranceFailedAttempts = 0;
                return null;
            }

            OnTokenAcquired(token);
            TokenAcquiranceFailedAttempts = 0;

            return token;
        }

        /// <summary>
        /// Будет качать пока не остановят
        /// </summary>
        public async void Start()
        {
            var currentToken = cancellationTokenSourceLoop.Token;

            if (Access == null)
            {
                Access = await RequestTokenToTheLimit(currentToken);

                if (Access == null)
                    return;
            }

            var usherUri = UsherNet.CreateUsherUri(channel, Access.signature, Access.value, settings.fastBread, SessionId, random);

            StartMasterLoop(usherUri, currentToken);
        }

        /// <summary>
        /// Остановить круги, но не ломать
        /// </summary>
        public void Stop()
        {
            cancellationTokenSourceLoop.Cancel();
            cancellationTokenSourceLoop = new();

            //LastMediaTime = null;
            //LastMediaSequenceNumber = -1; нет же смысла. даже если фастбред включён, первые сегменты дадут время
        }

        private async void StartMasterLoop(Uri usherUri, CancellationToken cancellationToken)
        {
            //ну вот мало ли выскочит непонятная ошибка, и ждать плейлист не будет в итоге
            DateTime? lastMasterPlaylistRequestDate = null;

            while (!Disposed && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (lastMasterPlaylistRequestDate != null)
                    {
                        var passed = DateTime.UtcNow - lastMasterPlaylistRequestDate.Value;

                        if (passed < settings.masterPlaylistRetryDelay)
                        {
                            var toWait = settings.masterPlaylistRetryDelay - passed;
                            try { await Task.Delay(settings.masterPlaylistRetryDelay, cancellationToken); } catch { return; }
                        }
                    }

                    string responseContent;
                    using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, usherUri))
                    {
                        requestMessage.Headers.Add("Accept", "application/x-mpegURL, application/vnd.apple.mpegurl, application/json, text/plain");

                        using var response = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead, cancellationToken);
                        responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            throw new BadCodeException(response.StatusCode, responseContent);
                        }
                    }
                    lastMasterPlaylistRequestDate = DateTime.UtcNow;

                    MasterParser parser = new();
                    parser.UnknownLineFound += (_, line) => OnUnknownPlaylistLine(true, line);
                    parser.CommentLineFound += (_, line) => OnCommentPlaylistLine(true, line);

                    MasterPlaylist playlist = parser.Parse(responseContent);

                    var twitchInfoTagInfo = playlist.globalTags.First(t => string.Equals(t.tag, "#EXT-X-TWITCH-INFO", StringComparison.Ordinal) /*t.tag == "#EXT-X-TWITCH-INFO"*/);
                    var twitchInfoTag = new XTwitchInfoTag(twitchInfoTagInfo.value!);

                    LastMasterPlaylistPrint = new MasterPlaylistPrint(DateTime.UtcNow, twitchInfoTag.streamTime);

                    MasterPlaylistDebugHandler?.Invoke(playlist);

                    try
                    {
                        await MediaLoopAsync(playlist, cancellationToken);
                    }
                    catch (TaskCanceledException) when (Disposed || cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (NoQualityException e)
                    {
                        //Вообще на той стороне человечек должен дёрнуть рубильник, но мало ли
                        OnMediaPlaylistException(e);

                        try { await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken); } catch { return; }
                        continue;
                    }
                    catch (Exception e)
                    {
                        OnMediaPlaylistException(e);
                        continue;
                    }

                    //если не вылет, значит закончился
                    OnPlaylistEnded();
                }
                catch (TaskCanceledException) when (Disposed || cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (BadCodeException e) when (e.statusCode == HttpStatusCode.Forbidden && settings.automaticallyUpdateAccessToken)
                {
                    OnMasterPlaylistException(e);

                    Access = await RequestTokenToTheLimit(cancellationToken);
                    if (Access == null)
                        return;

                    usherUri = UsherNet.CreateUsherUri(channel, Access.signature, Access.value, settings.fastBread, SessionId, random);
                }
                catch (Exception e)
                {
                    OnMasterPlaylistException(e);

                    //непонятно зачем это теперь, ну да ладно
                    try { await Task.Delay(settings.masterPlaylistRetryDelay, cancellationToken); } catch { return; }
                }
            }
        }

        private async Task MediaLoopAsync(MasterPlaylist masterPlaylist, CancellationToken cancellationToken)
        {
            VariantStream? variantStream = null;

            if (LastVideo != null)
            {
                variantStream = masterPlaylist.variantStreams.FirstOrDefault(s => string.Equals(s.streamInfTag.video, LastVideo, StringComparison.Ordinal) /*s.streamInfTag.video == LastVideo*/);
            }

            if (variantStream == null && settings.preferredQuality != null)
            {
                //video вроде всегда есть

                //формат video выглядит как 720p60
                VariantStream[] qualityStreams = masterPlaylist.variantStreams.Where(s => s.streamInfTag.video!.StartsWith(settings.preferredQuality))
                                                                              .ToArray();

                if (settings.preferredFps != null)
                {
                    variantStream = qualityStreams.FirstOrDefault(s => s.streamInfTag.video!.EndsWith(settings.preferredFps));

                    if (variantStream == null)
                        variantStream = qualityStreams.FirstOrDefault();
                }
                else
                {
                    variantStream = qualityStreams.FirstOrDefault();
                }

                if (variantStream == null && settings.takeOnlyPreferredQuality)
                {
                    var videos = masterPlaylist.variantStreams.Select(s => s.streamInfTag.video!).ToArray();

                    throw new NoQualityException(videos);
                }
            }

            if (variantStream == null)
                variantStream = masterPlaylist.variantStreams.First();

            OnMediaQualitySelected(variantStream);
            LastVideo = variantStream.streamInfTag.video!;

            while (!Disposed && !cancellationToken.IsCancellationRequested)
            {
                string responseContent;
                using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, variantStream.uri))
                {
                    using HttpResponseMessage responseMessage = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead, cancellationToken);
                    responseContent = await responseMessage.Content.ReadAsStringAsync(cancellationToken);

                    if (responseMessage.StatusCode != HttpStatusCode.OK)
                    {
                        throw new BadCodeException(responseMessage.StatusCode, responseContent);
                    }
                }

                MediaParser parser = new();
                parser.UnknownLineFound += (_, line) => OnUnknownPlaylistLine(false, line);
                parser.CommentLineFound += (_, line) => OnCommentPlaylistLine(false, line);

                MediaPlaylist mediaPlaylist = parser.Parse(responseContent);

                LastMediaPlaylistUpdate = DateTime.UtcNow;

                MediaPlaylistDebugHandler?.Invoke(mediaPlaylist);

                int firstMediaSequenceNumber = mediaPlaylist.mediaSequenceTag?.mediaSequenceNumber ?? 0;

                for (int index = 0; index < mediaPlaylist.mediaSegments.Count; index++)
                {
                    int currentMediaSequenceNumber = firstMediaSequenceNumber + index;

                    MediaSegment mediaSegment = mediaPlaylist.mediaSegments[index];

                    /* такое возможно, если выбран режим быстрого хлеба
                     * впадлу думать. TODO подумать. можно качать эти сегменты, и если потом они находятся иным путём... или если вместо них приходит реклама... */
                    if (mediaSegment.programDateTag == null)
                    {
                        continue;
                    }

                    if (currentMediaSequenceNumber <= LastMediaSequenceNumber && LastMediaTime >= mediaSegment.programDateTag.time) continue;

                    LastMediaTime = mediaSegment.programDateTag.time;
                    LastMediaSequenceNumber = currentMediaSequenceNumber;

                    //title вроде всегда есть
                    StreamSegment segmentInfo = new(mediaSegment.uri, mediaSegment.infTag.title!, currentMediaSequenceNumber, mediaSegment.infTag.duration, mediaSegment.programDateTag.time, LastVideo);

                    OnSegmentArrived(segmentInfo);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }

                OnMediaPlaylistProcessed();

                if (mediaPlaylist.endList)
                {
                    //зачем это обнулять? время же не движется назать блять
                    //LastMediaTime = null;
                    LastMediaSequenceNumber = -1;
                    return;
                }

                int resultDuration = (int)(mediaPlaylist.mediaSegments.Sum(s => s.infTag.duration) / 2f * 1000);

                if (resultDuration > settings.maxMediaPlaylistUpdateDelay.TotalMilliseconds)
                    resultDuration = (int)settings.maxMediaPlaylistUpdateDelay.TotalMilliseconds;

                if (resultDuration < settings.minMediaPlaylistUpdateDelay.TotalMilliseconds)
                    resultDuration = (int)settings.minMediaPlaylistUpdateDelay.TotalMilliseconds;

                await Task.Delay(resultDuration, cancellationToken);
            }
        }

        /// <summary>
        /// Отказатсья от токена, чтобы загрузчик остановился и попытался получить новый и продолжить.
        /// По факту ленивый шорткат от Stop Access=null Start
        /// </summary>
        public void DropToken()
        {
            cancellationTokenSourceLoop.Cancel();
            cancellationTokenSourceLoop = new();

            Access = null;

            Start();
        }

        #region Events
        private void OnPlaylistEnded()
        {
            PlaylistEnded?.Invoke(this, EventArgs.Empty);
        }

        private void OnMasterPlaylistException(Exception e)
        {
            MasterPlaylistExceptionOccured?.Invoke(this, e);
        }

        private void OnMediaPlaylistException(Exception e)
        {
            MediaPlaylistExceptionOccured?.Invoke(this, e);
        }

        private void OnMediaQualitySelected(VariantStream variantStream)
        {
            MediaQualitySelected?.Invoke(this, variantStream);
        }

        private void OnSegmentArrived(StreamSegment segmentInfo)
        {
            SegmentArrived?.Invoke(this, segmentInfo);
        }

        private void OnMediaPlaylistProcessed()
        {
            MediaPlaylistProcessed?.Invoke(this, EventArgs.Empty);
        }

        private void OnUnknownPlaylistLine(bool master, string line)
        {
            UnknownPlaylistLineFound?.Invoke(this, new LineEventArgs(master, line));
        }

        private void OnCommentPlaylistLine(bool master, string line)
        {
            CommentPlaylistLineFound?.Invoke(this, new LineEventArgs(master, line));
        }

        private void OnTokenAcquired(AccessToken accessToken)
        {
            TokenAcquired?.Invoke(this, accessToken);
        }
        #endregion

        public void Dispose()
        {
            if (Disposed)
                return;

            Disposed = true;

            try { cancellationTokenSourceWeb.Cancel(); } catch { }
            try { cancellationTokenSourceLoop.Cancel(); } catch { }

            cancellationTokenSourceWeb.Dispose();
            cancellationTokenSourceLoop.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}