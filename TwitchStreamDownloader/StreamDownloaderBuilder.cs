using Microsoft.Extensions.Logging;
using TwitchStreamDownloader.Download;

namespace TwitchStreamDownloader;

public class StreamDownloaderBuilder
{
    private readonly string channel;

    private SegmentsDownloaderSettings? settings;

    private string? clientId;
    private string? oauth;

    private TimeSpan? downloadTimeout;

    private ILogger? logger;
    private HttpClient? httpClient;

    private Action<StreamSelection>? action;

    public StreamDownloaderBuilder(string channel)
    {
        this.channel = channel;
    }

    public StreamDownloaderBuilder WithSettings(SegmentsDownloaderSettings? settings)
    {
        this.settings = settings;
        return this;
    }

    /// <summary>
    /// По умолчанию таймаут 5 секунд.
    /// </summary>
    /// <param name="timeout"></param>
    /// <returns></returns>
    public StreamDownloaderBuilder WithDownloadTimeout(TimeSpan? timeout)
    {
        this.downloadTimeout = timeout;

        return this;
    }

    public StreamDownloaderBuilder WithAuth(string? clientId, string? oauth)
    {
        this.clientId = clientId;
        this.oauth = oauth;

        return this;
    }

    public StreamDownloaderBuilder WithLogger(ILogger? logger)
    {
        this.logger = logger;

        return this;
    }

    public StreamDownloaderBuilder WithHttpClient(HttpClient? client)
    {
        this.httpClient = client;

        return this;
    }

    public StreamDownloaderBuilder WithStreamSelectionOverride(Action<StreamSelection> action)
    {
        this.action = action;

        return this;
    }

    public StreamDownloader Build()
    {
        settings ??= new SegmentsDownloaderSettings();

        downloadTimeout ??= TimeSpan.FromSeconds(5);

        httpClient ??= new HttpClient(new HttpClientHandler()
        {
            Proxy = null,
            UseProxy = false
        });

        return new StreamDownloader(channel, settings, clientId, oauth, downloadTimeout.Value, httpClient, logger,
            action);
    }
}