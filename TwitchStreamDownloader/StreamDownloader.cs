using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchStreamDownloader.Download;
using TwitchStreamDownloader.Net;
using TwitchStreamDownloader.Queues;
using TwitchStreamDownloader.Resources;

namespace TwitchStreamDownloader;

public class StreamDownloader
{
    public SegmentsDownloader SegmentsDownloader { get; }
    public DownloadQueue DownloadQueue { get; }

    public event Action<QueueItem, Exception>? SegmentDownloadExceptionOccured;

    readonly HttpClient httpClient;

    /// <summary>
    /// По умолчанию false.
    /// </summary>
    public bool DownloadAdvertisment { get; set; } = false;

    /// <summary>
    /// Твич даёт токен длй загрузки стрима на какое-то времй. Можно самому заранее его сбрасывать и брать новый, чтобы посреди загрузки не пропал доступ неожиданно.
    /// По умолчанию true.
    /// </summary>
    public bool DownloaderForceTokenChange { get; set; } = true;

    public bool Working { get; private set; }

    public StreamDownloader(string channel, SegmentsDownloaderSettings segmentsDownloaderSettings, string? clientId, string? oauth, TimeSpan downloadQueueTimeout, HttpClient httpClient)
    {
        this.httpClient = httpClient;

        SegmentsDownloader = new SegmentsDownloader(httpClient, segmentsDownloaderSettings, channel, clientId, oauth);
        SegmentsDownloader.TokenAcquired += TokenAcquired;
        SegmentsDownloader.SegmentArrived += SegmentArrived;

        DownloadQueue = new DownloadQueue(downloadQueueTimeout);
    }

    public void Start()
    {
        Working = true;

        SegmentsDownloader.Start();
    }

    public void Suspend()
    {
        Working = false;

        SegmentsDownloader.Stop();
    }

    public void Resume()
    {
        Working = true;

        SegmentsDownloader.Start();
    }

    public void Close()
    {
        SegmentsDownloader.Dispose();
        DownloadQueue.Dispose();

        httpClient.Dispose();
    }

    private async void SegmentArrived(object? sender, StreamSegment segment)
    {
        if (!segment.IsLive && !DownloadAdvertisment)
            return;

        QueueItem queueItem = DownloadQueue.Queue(segment, new MemoryStream());
        try
        {
            await DownloadQueue.DownloadAsync(httpClient, queueItem);
        }
        catch (Exception e)
        {
            SegmentDownloadExceptionOccured?.Invoke(queueItem, e);
        }
    }

    private void TokenAcquired(object? sender, AccessToken e)
    {
        var downloader = (SegmentsDownloader)sender!;

        if (e.parsedValue.expires == null)
            return;

        var left = DateTimeOffset.FromUnixTimeSeconds(e.parsedValue.expires.Value) - DateTimeOffset.UtcNow;

        if (DownloaderForceTokenChange)
        {
            Task.Run(async () =>
            {
                // в тевории стрим может уже закончится, кстати.
                // но один лишний таск это не проблема, я думаю
                // TODO Добавить локов, чтобы исключить околоневозможный шанс пересечения интересов
                await Task.Delay(left - TimeSpan.FromSeconds(5));

                if (downloader.Access != e)
                    return;

                //по факту лишние проверки, ну да ладно
                if (downloader.Disposed || !Working)
                    return;

                downloader.DropToken();
            });
        }
    }
}
