using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchStreamDownloader.Download;

namespace TwitchStreamDownloader;

public class StreamDownloaderBuilder
{
    readonly string channel;

    SegmentsDownloaderSettings? settings;

    string? clientId;
    string? oauth;

    TimeSpan? downloadTimeout;

    HttpClient? httpClient;

    public StreamDownloaderBuilder(string channel)
    {
        this.channel = channel;
    }

    public StreamDownloaderBuilder WithSettings(SegmentsDownloaderSettings settings)
    {
        this.settings = settings;
        return this;
    }

    /// <summary>
    /// По умолчанию таймаут 5 секунд.
    /// </summary>
    /// <param name="timeout"></param>
    /// <returns></returns>
    public StreamDownloaderBuilder WithDownloadTimeout(TimeSpan timeout)
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

    public StreamDownloaderBuilder WithHttpClient(HttpClient client)
    {
        this.httpClient = client;

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

        return new StreamDownloader(channel, settings, clientId, oauth, downloadTimeout.Value, httpClient);
    }
}
