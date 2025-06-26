using System;
using System.Net;
using ExtM3UPlaylistParser.Models;
using ExtM3UPlaylistParser.Parsers;
using ExtM3UPlaylistParser.Playlists;
using ExtM3UPlaylistParser.Tags.Master;
using TwitchStreamDownloader.Exceptions;
using TwitchStreamDownloader.Extensions;
using TwitchStreamDownloader.Net;
using TwitchStreamDownloader.Resources;

namespace TwitchStreamDownloader.Download;

public class TokenAcquiringExceptionEventArgs : EventArgs
{
    public Exception Exception { get; set; }
    public int Attempts { get; set; }

    public TokenAcquiringExceptionEventArgs(Exception exception, int attempts)
    {
        Exception = exception;
        Attempts = attempts;
    }
}

public class MediaQualitySelectedEventArgs : EventArgs
{
    public VariantStream VariantStream { get; set; }
    public Quality Quality { get; set; }

    public MediaQualitySelectedEventArgs(VariantStream variantStream, Quality quality)
    {
        VariantStream = variantStream;
        Quality = quality;
    }
}

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
/// Этот класс просто качает плейлист и выдаёт сегменты в порядке очереди.
/// Сегмент в данном случае это инфа о сегменте видео, а не сами видео.
/// </summary>
public class SegmentsDownloader : IDisposable
{
    public bool Disposed { get; private set; } = false;

    /// <summary>
    /// програмдейт последнего сегмента
    /// </summary>
    public DateTimeOffset? LastMediaTime { get; private set; } = null;

    /// <summary>
    /// Последний номер в текущей загрузки. Если mediaplaylist сменился, становится null
    /// </summary>
    public int? LastMediaSequenceNumber { get; private set; } = null;

    public MasterPlaylistPrint? LastMasterPlaylistPrint { get; private set; }

    /// <summary>
    /// UTC
    /// </summary>
    public DateTime? LastMediaPlaylistUpdate { get; private set; } = null;

    public Quality? LastStreamQuality { get; private set; } = null;

    //Предполагалось, что сюда можно будет руками из кеша положить, но мне стало впадлу.
    public AccessToken? Access { get; private set; }
    public string DeviceId { get; private set; }
    public string SessionId { get; private set; }

    public readonly HttpClient httpClient;

    /// <summary>
    /// Токен отмены текущего круга загрузки сегментов
    /// </summary>
    private CancellationTokenSource? cancellationTokenSourceLoop;

    private readonly Random random = new();

    private readonly SegmentsDownloaderSettings settings;
    private readonly string channel;
    private string? clientId;
    private string? oauth;

    public event EventHandler? PlaylistEnded;
    public event EventHandler<Exception>? MasterPlaylistExceptionOccured;
    public event EventHandler<Exception>? MediaPlaylistExceptionOccured;

    /// <summary>
    /// <see cref="LastStreamQuality"/> содержит предыдущее качество
    /// </summary>
    public event EventHandler<MediaQualitySelectedEventArgs>? MediaQualitySelected;

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
    public event EventHandler<TokenAcquiringExceptionEventArgs>? TokenAcquiringExceptionOccured;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="httpClient">Не забудь его задиспоузить сам.</param>
    /// <param name="settings"></param>
    /// <param name="channel"></param>
    /// <param name="clientId"></param>
    /// <param name="oauth"></param>
    public SegmentsDownloader(HttpClient httpClient, SegmentsDownloaderSettings settings, string channel,
        string? clientId, string? oauth)
    {
        this.settings = settings;
        this.channel = channel;
        this.clientId = clientId;
        this.oauth = oauth;

        this.httpClient = httpClient;

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
    public async Task<AccessToken> RequestToken(CancellationToken cancellationToken)
    {
        const string undefined = "undefined";

        string requestClientId = clientId ?? "kimne78kx3ncx6brgo4mv6wki5h1ko";
        string requestOauth = oauth ?? undefined;

        return await GqlNet.GetAccessToken(httpClient, channel, requestClientId, DeviceId, requestOauth,
            cancellationToken);
    }

    /// <summary>
    /// Изо всех сил пытается достать токен. Ошибки не бросает. Ну, хендлеры могут в тевории.
    /// </summary>
    /// <returns>нулл, если круг отменился</returns>
    async Task<AccessToken?> RequestTokenToTheLimit(CancellationToken cancellationToken)
    {
        int tokenAcquiranceFailedAttempts = 0;
        AccessToken? token = null;

        while (token == null && !cancellationToken.IsCancellationRequested && !Disposed)
        {
            try
            {
                token = await RequestToken(cancellationToken);
            }
            catch (Exception e)
            {
                tokenAcquiranceFailedAttempts++;

                TokenAcquiringExceptionOccured?.Invoke(this,
                    new TokenAcquiringExceptionEventArgs(e, tokenAcquiranceFailedAttempts));

                bool shortDelay;
                if (oauth != null && settings.OauthTokenFailedAttemptsLimit != -1 &&
                    tokenAcquiranceFailedAttempts >= settings.OauthTokenFailedAttemptsLimit)
                {
                    SetCreds(null, null);

                    shortDelay = true;
                }
                else
                {
                    // Первую ошибку долго не ждём, мб серверу поплохело просто.
                    // И да, это реальное решение проблемы, такое бывает.

                    shortDelay = tokenAcquiranceFailedAttempts == 1;
                }

                TimeSpan delay = shortDelay ? settings.ShortAccessTokenRetryDelay : settings.AccessTokenRetryDelay;
                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch
                {
                    break;
                }
            }
        }

        //Тупо, что 2 проверки. Но они дешёвые, а алгоритм получше ещё придумать нужно.
        if (token == null || cancellationToken.IsCancellationRequested || Disposed)
        {
            return null;
        }

        OnTokenAcquired(token);

        return token;
    }

    /// <summary>
    /// Будет качать пока не остановят
    /// </summary>
    public void Start()
    {
        var ctsl = cancellationTokenSourceLoop = new();

        _ = Task.Run(() => InitiateAsync(ctsl.Token));
    }

    /// <summary>
    /// Остановить круги, но не ломать
    /// </summary>
    public void Stop()
    {
        if (cancellationTokenSourceLoop != null && !cancellationTokenSourceLoop.IsCancellationRequested)
        {
            cancellationTokenSourceLoop.Cancel();
            try
            {
                cancellationTokenSourceLoop.Dispose();
            }
            catch
            {
            }
        }
        else
        {
            // Вообще, это плохо, наверное. Ну ладно.
        }
    }

    private async Task InitiateAsync(CancellationToken cancellationToken)
    {
        if (Access == null)
        {
            Access = await RequestTokenToTheLimit(cancellationToken);

            if (Access == null)
                return;
        }

        var usherUri = UsherNet.CreateUsherUri(channel, Access.signature, Access.value, settings.FastBread, SessionId,
            random);

        _ = Task.Run(() => MasterLoopAsync(usherUri, cancellationToken), cancellationToken);
    }

    private async Task MasterLoopAsync(Uri usherUri, CancellationToken cancellationToken)
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

                    if (passed < settings.MasterPlaylistRetryDelay)
                    {
                        var toWait = settings.MasterPlaylistRetryDelay - passed;
                        try
                        {
                            await Task.Delay(settings.MasterPlaylistRetryDelay, cancellationToken);
                        }
                        catch
                        {
                            return;
                        }
                    }
                }

                string responseContent;
                using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, usherUri))
                {
                    requestMessage.Headers.Add("Accept",
                        "application/x-mpegURL, application/vnd.apple.mpegurl, application/json, text/plain");

                    using var response = await httpClient.SendAsync(requestMessage,
                        HttpCompletionOption.ResponseContentRead, cancellationToken);
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

                var twitchInfoTagInfo = playlist.globalTags.First(
                    t => string.Equals(t.tag, "#EXT-X-TWITCH-INFO",
                        StringComparison.Ordinal) /*t.tag == "#EXT-X-TWITCH-INFO"*/);
                var twitchInfoTag = new XTwitchInfoTag(twitchInfoTagInfo.value!);

                LastMasterPlaylistPrint = new MasterPlaylistPrint(DateTime.UtcNow, twitchInfoTag.streamTime);

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

                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                    }
                    catch
                    {
                        return;
                    }

                    continue;
                }
                catch (Exception e)
                {
                    OnMediaPlaylistException(e);
                    continue;
                }
                finally
                {
                    LastMediaSequenceNumber = null;
                }

                //если не вылет, значит закончился
                OnPlaylistEnded();
            }
            catch (TaskCanceledException) when (Disposed || cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (BadCodeException e) when (e.statusCode == HttpStatusCode.Forbidden &&
                                             settings.AutomaticallyUpdateAccessToken)
            {
                OnMasterPlaylistException(e);

                Access = await RequestTokenToTheLimit(cancellationToken);
                if (Access == null)
                    return;

                usherUri = UsherNet.CreateUsherUri(channel, Access.signature, Access.value, settings.FastBread,
                    SessionId, random);
            }
            catch (Exception e)
            {
                OnMasterPlaylistException(e);

                //непонятно зачем это теперь, ну да ладно
                try
                {
                    await Task.Delay(settings.MasterPlaylistRetryDelay, cancellationToken);
                }
                catch
                {
                    return;
                }
            }
        }
    }

    private async Task MediaLoopAsync(MasterPlaylist masterPlaylist, CancellationToken cancellationToken)
    {
        VariantStream? variantStream = null;

        if (LastStreamQuality != null)
        {
            // Есть аудиоонли, да
            variantStream = masterPlaylist.variantStreams.FirstOrDefault(s =>
                ResolutionExtensions.Compare(s.streamInfTag.resolution, LastStreamQuality.Resolution) &&
                s.streamInfTag.frameRate == LastStreamQuality.Fps);
        }

        if (variantStream == null && settings.PreferredResolution != null)
        {
            VariantStream[] qualityStreams = masterPlaylist.variantStreams
                .Where(s => settings.PreferredResolution.Same(s.streamInfTag.resolution))
                .ToArray();

            if (settings.PreferredFps != null)
            {
                variantStream = qualityStreams.FirstOrDefault(s => s.streamInfTag.frameRate == settings.PreferredFps);

                variantStream ??= qualityStreams.FirstOrDefault();
            }
            else
            {
                variantStream = qualityStreams.FirstOrDefault();
            }

            if (variantStream == null && settings.TakeOnlyPreferredQuality)
            {
                var videos = masterPlaylist.variantStreams
                    .Select(s => $"{s.streamInfTag.resolution} {s.streamInfTag.frameRate}").ToArray();

                throw new NoQualityException(videos);
            }
        }

        variantStream ??= masterPlaylist.variantStreams.First();

        // Ну они вроде всегда есть.
        var quality = new Quality(variantStream.streamInfTag.resolution, variantStream.streamInfTag.frameRate!.Value);

        OnMediaQualitySelected(variantStream, quality);
        LastStreamQuality = quality;

        while (!Disposed && !cancellationToken.IsCancellationRequested)
        {
            string responseContent;
            using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, variantStream.uri))
            {
                using HttpResponseMessage responseMessage = await httpClient.SendAsync(requestMessage,
                    HttpCompletionOption.ResponseContentRead, cancellationToken);
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

                if (LastMediaTime >= mediaSegment.programDateTag.time) continue;
                if (currentMediaSequenceNumber <= LastMediaSequenceNumber) continue;

                LastMediaTime = mediaSegment.programDateTag.time;
                LastMediaSequenceNumber = currentMediaSequenceNumber;

                TagInfo? mapTag = mediaSegment.unAddedTags.FirstOrDefault(t => t.tag == "#EXT-X-MAP");

                //title вроде всегда есть
                StreamSegment segmentInfo = new(mediaSegment.uri, mediaSegment.infTag.title, currentMediaSequenceNumber,
                    mediaSegment.infTag.duration, mediaSegment.programDateTag.time, LastStreamQuality, mapTag?.value);

                OnSegmentArrived(segmentInfo);

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }

            OnMediaPlaylistProcessed();

            if (mediaPlaylist.endList)
            {
                return;
            }

            int resultDuration = (int)(mediaPlaylist.mediaSegments.Sum(s => s.infTag.duration) / 2f * 1000);

            if (resultDuration > settings.MaxMediaPlaylistUpdateDelay.TotalMilliseconds)
                resultDuration = (int)settings.MaxMediaPlaylistUpdateDelay.TotalMilliseconds;

            if (resultDuration < settings.MinMediaPlaylistUpdateDelay.TotalMilliseconds)
                resultDuration = (int)settings.MinMediaPlaylistUpdateDelay.TotalMilliseconds;

            await Task.Delay(resultDuration, cancellationToken);
        }
    }

    /// <summary>
    /// Отказатсья от токена, чтобы загрузчик остановился и попытался получить новый и продолжить.
    /// По факту ленивый шорткат от Stop Access=null Start
    /// </summary>
    public void DropToken()
    {
        Stop();

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

    private void OnMediaQualitySelected(VariantStream variantStream, Quality quality)
    {
        MediaQualitySelected?.Invoke(this, new MediaQualitySelectedEventArgs(variantStream, quality));
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

        if (cancellationTokenSourceLoop != null)
        {
            try
            {
                cancellationTokenSourceLoop.Cancel();
            }
            catch
            {
            }

            cancellationTokenSourceLoop.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}