using TwitchStreamDownloader.Download;
using TwitchStreamDownloader.Exceptions;
using TwitchStreamDownloader.Resources;

namespace TwitchStreamDownloader.Queues;

/// <summary>
/// Этот класс отвечает за загрузку сегментов из интернета и выдачу их по порядку.
/// Типа более поздний сегмент может скачаться раньше...
/// Сначала нужно задавать очередь, а потом загружать.
/// </summary>
public class DownloadQueue : IDisposable
{
    public bool Disposed { get; private set; }

    private readonly object locker = new();

    private readonly TimeSpan downloadTimeout;

    private readonly Queue<QueueItem> queue = new();

    private bool processing = false;

    /// <summary>
    /// Когда сегмент скачался или затаймаутился. В порядке очереди.
    /// </summary>
    public event EventHandler<QueueItem>? ItemDequeued;

    public DownloadQueue(TimeSpan downloadTimeout)
    {
        this.downloadTimeout = downloadTimeout;
    }

    /// <summary>
    /// Эта штука кидает ошибки, если чето не загрузится.
    /// Использовать после Queue
    /// </summary>
    /// <exception cref="Exception">Куча всего.</exception>
    public async Task DownloadAsync(HttpClient httpClient, QueueItem queueItem)
    {
        try
        {
            using var downloadCts = new CancellationTokenSource(downloadTimeout);

            await DownloadVideoAsync(httpClient, queueItem.segment.uri, queueItem.bufferWriteStream, downloadCts.Token);

            queueItem.SetWritten();
        }
        catch
        {
            queueItem.SetNotWritten();
            throw;
        }
    }

    /// <summary>
    /// Кладёт в очередь.
    /// </summary>
    public QueueItem Queue(StreamSegment segment, Stream bufferWriteStream)
    {
        var item = new QueueItem(segment, bufferWriteStream);

        lock (locker)
        {
            queue.Enqueue(item);

            if (processing)
                return item;

            processing = true;
        }

        _ = Task.Run(ProcessingLoopAsync);

        return item;
    }

    async Task ProcessingLoopAsync()
    {
        while (!Disposed)
        {
            QueueItem? item;
            lock (locker)
            {
                if (!queue.TryDequeue(out item))
                {
                    processing = false;
                    return;
                }
            }

            await item.DownloadTask;

            ItemDequeued?.Invoke(this, item);
        }
    }

    /// <exception cref="TaskCanceledException">Токен отменился.</exception>
    /// <exception cref="Exception">Куча всего.</exception>
    async Task DownloadVideoAsync(HttpClient httpClient, Uri uri, Stream writeStream, CancellationToken timeoutCancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead, timeoutCancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string responseContent = await response.Content.ReadAsStringAsync(CancellationToken.None);

            throw new BadCodeException(response.StatusCode, responseContent);
        }

        await response.Content.CopyToAsync(writeStream, timeoutCancellationToken);
    }

    public void Dispose()
    {
        if (Disposed)
            return;

        Disposed = true;

        lock (locker)
        {
            //если остались непонятные ливы, их непонятные ресы нужно задиспоузить, мало ли
            foreach (var q in queue)
            {
                q.bufferWriteStream.Dispose();
            }
            queue.Clear();
        }

        GC.SuppressFinalize(this);
    }
}
