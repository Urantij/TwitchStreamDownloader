using TwitchStreamDownloader.Download;
using TwitchStreamDownloader.Exceptions;
using TwitchStreamDownloader.Resources;

namespace TwitchStreamDownloader.Queues
{
    public class QueueItem
    {
        public readonly StreamSegment segment;

        /// <summary>
        /// Загрузка будет происходит сразу, а не в порядке очереди, если чо.
        /// Так что копировать нужно в какой-то буфер.
        /// А потом из буфера куда надо, когда очередь подойдёт.
        /// </summary>
        public readonly Stream bufferWriteStream;

        /// <summary>
        /// Произошла ли запись в буффер.
        /// Может быть и произошла, кстати.
        /// </summary>
        public bool Written { get; private set; } = false;

        public Task DownloadTask => tcs.Task;

        readonly TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public QueueItem(StreamSegment segment, Stream bufferWriteStream)
        {
            this.segment = segment;
            this.bufferWriteStream = bufferWriteStream;
        }

        public void SetWritten()
        {
            Written = true;
            tcs.SetResult();
        }

        public void SetNotWritten()
        {
            Written = false;
            tcs.SetResult();
        }
    }

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

        //тута в очереди
        private readonly Queue<QueueItem> queue = new();

        private bool processing = false;

        /// <summary>
        /// Когда хуйня скачалась или затаймаутилась. в порядке очереди.
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
            //наверное, если тут уже пошла загрузка, отменять не супер идея, но с другой стороны, хуй знает че там и как
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
}