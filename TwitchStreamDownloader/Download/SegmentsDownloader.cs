using System.Net;
using PlaylistParser.Models;
using PlaylistParser.Parsers;
using PlaylistParser.Playlists;
using PlaylistParser.Tags.Master;
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

        private readonly HttpClient client;
        //Вообще, раз уж качамба отдельно идёт, нужно бы вынести её. Но да ладно
        private CancellationTokenSource cancellationTokenSourceWeb;
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
        /// Ошибка именно при загрузке контента сегмента
        /// </summary>
        public event EventHandler<Exception>? SegmentDownloadExceptionOccured;
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
        public event EventHandler<Exception>? TokenAcquiringException;

        //дыбажим
        public event Action<MasterPlaylist>? MasterPlaylistDebugHandler;
        public event Action<MediaPlaylist>? MediaPlaylistDebugHandler;

        public SegmentsDownloader(SegmentsDownloaderSettings settings, string channel, string? clientId, string? oauth)
        {
            this.settings = settings;
            this.channel = channel;
            this.clientId = clientId;
            this.oauth = oauth;

            var httpHandler = new SocketsHttpHandler()
            {
                Proxy = settings.proxy,
            };
            httpHandler.UseProxy = settings.proxy != null;

            client = new HttpClient(httpHandler)
            {
                Timeout = TimeSpan.FromSeconds(7.5)
            };
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("User-Agent", settings.userAgent);

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

            var access = await GqlNet.GetAccessToken(client, channel, requestClientId, DeviceId, requestOauth, cancellationTokenSourceLoop.Token);

            return access;
        }

        /// <summary>
        /// Изо всех сил пытается достать токен. Ошибки не бросает. Ну, хендлеры могут в тевории.
        /// </summary>
        /// <returns>нулл, если хуйня отменилась</returns>
        async Task<AccessToken?> RequestTokenToTheLimit(CancellationToken cancellationToken)
        {
            AccessToken? token = null;

            while (token == null && !cancellationToken.IsCancellationRequested && !Disposed)
            {
                try
                {
                    token = await RequestToken();
                }
                catch (Exception e)
                {
                    TokenAcquiringException?.Invoke(this, e);

                    try { await Task.Delay(settings.accessTokenRetryDelay, cancellationToken); } catch { return null; }
                }
            }

            //Тупо, что 2 проверки. Но они дешёвые, а алгоритм получше ещё придумать нужно.
            if (token == null || cancellationToken.IsCancellationRequested || Disposed)
                return null;

            TokenAcquired?.Invoke(this, token);

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
            LastMediaSequenceNumber = -1;
        }

        private async void StartMasterLoop(Uri usherUri, CancellationToken cancellationToken)
        {
            while (!Disposed && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    string responseContent;
                    using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, usherUri))
                    {
                        requestMessage.Headers.Add("Accept", "application/x-mpegURL, application/vnd.apple.mpegurl, application/json, text/plain");

                        using var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead, cancellationToken);
                        responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            throw new BadCodeException(response.StatusCode, responseContent);
                        }
                    }

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
                        await MediaLoop(playlist, cancellationToken);
                        //если не вылет, значит закончился
                        OnPlaylistEnded();
                    }
                    catch (TaskCanceledException) when (Disposed || cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception e)
                    {
                        OnMediaPlaylistException(e);
                    }
                }
                catch (TaskCanceledException) when (Disposed || cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (BadCodeException bde) when (bde.statusCode == HttpStatusCode.Forbidden && settings.automaticallyUpdateAccessToken)
                {
                    OnMasterPlaylistException(bde);

                    Access = await RequestTokenToTheLimit(cancellationToken);
                    if (Access == null)
                        return;

                    usherUri = UsherNet.CreateUsherUri(channel, Access.signature, Access.value, settings.fastBread, SessionId, random);
                }
                catch (Exception e)
                {
                    OnMasterPlaylistException(e);

                    try { await Task.Delay(settings.masterPlaylistRetryDelay, cancellationToken); } catch { return; }
                }
            }
        }

        private async Task MediaLoop(MasterPlaylist masterPlaylist, CancellationToken cancellationToken)
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
                    using HttpResponseMessage responseMessage = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead, cancellationToken);
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

        /// <param name="uri"></param>
        /// <param name="writeStream"></param>
        /// <param name="token"></param>
        /// <exception cref="TaskCanceledException">Токен отменился.</exception>
        public async Task<bool> TryDownload(Uri uri, Stream writeStream, CancellationToken token)
        {
            try
            {
                using var cancelSus = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSourceWeb.Token, token);
                using var requestMessage = new HttpRequestMessage(HttpMethod.Get, uri);
                using var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead, cancelSus.Token);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    string responseContent = await response.Content.ReadAsStringAsync(CancellationToken.None);

                    throw new BadCodeException(response.StatusCode, responseContent);
                }

                await response.Content.CopyToAsync(writeStream, cancelSus.Token);

                return true;
            }
            catch (TaskCanceledException) when (!token.IsCancellationRequested)
            {
                return false;
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                OnSegmentDownloadException(e);
                return false;
            }
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

        private void OnSegmentDownloadException(Exception e)
        {
            SegmentDownloadExceptionOccured?.Invoke(this, e);
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

            client.Dispose();
            try { cancellationTokenSourceWeb.Dispose(); } catch { }
            try { cancellationTokenSourceLoop.Dispose(); } catch { }

            GC.SuppressFinalize(this);
        }
    }
}