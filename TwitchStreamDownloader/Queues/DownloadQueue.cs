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

        public readonly Task downloadOperationTask;

        /// <summary>
        /// Произошла ли запись в буффер.
        /// Может быть и произошла, кстати.
        /// </summary>
        public bool written = false;

        public QueueItem(StreamSegment segment, Stream bufferWriteStream, Task downloadOperationTask)
        {
            this.segment = segment;
            this.bufferWriteStream = bufferWriteStream;
            this.downloadOperationTask = downloadOperationTask;
        }
    }

    /// <summary>
    /// Этот класс отвечает за загрузку сегментов из интернета и выдачу их по порядку.
    /// Типа более поздний сегмент может скачаться раньше...
    /// И значит сначала вызывается метод для вызова загрузки, а потом метод очереди.
    /// То есть метод очереди задаёт, собсо, порядок сегментов в очереди, а метод загрузки просто качает.
    /// Таким образом можно юзать очередь и для одного потока сегментов и для многих.
    /// И метод очереди должен юзаться ВСЕГДА после метода загрузки.
    /// И желательно, чтобы метод очереди тоже всегда юзался, или сегменты в загрузке будут до диспоуза отдыхать.
    /// </summary>
    public class DownloadQueue : IDisposable
    {
        public bool Disposed { get; private set; }

        private readonly object locker = new();

        private readonly TimeSpan downloadTimeout;

        /* лайв пришёл раньше очередного, пусть обтекает
         * в теории очередной может вообще не прийти, но блять */
        private readonly List<QueueItem> liveList = new();
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
        /// Queue в любом случае нужно будет использовать после.
        /// Иначе смэрть.
        /// </summary>
        /// <param name="httpClient"></param>
        /// <param name="segment"></param>
        /// <param name="bufferWriteStream"></param>
        /// <returns></returns>
        /// <exception cref="Exception">Куча всего.</exception>
        public async Task DownloadAsync(HttpClient httpClient, StreamSegment segment, Stream bufferWriteStream)
        {
            TaskCompletionSource taskSource = new();

            var item = new QueueItem(segment, bufferWriteStream, taskSource.Task);
            lock (locker)
            {
                liveList.Add(item);
            }

            //наверное, если тут уже пошла загрузка, отменять не супер идея, но с другой стороны, хуй знает че там и как
            try
            {
                using var downloadCts = new CancellationTokenSource(downloadTimeout);

                await DownloadVideoAsync(httpClient, segment.uri, bufferWriteStream, downloadCts.Token);

                item.written = true;
                taskSource.SetResult();
            }
            catch
            {
                taskSource.SetResult();
                throw;
            }
        }

        /// <summary>
        /// Кладёт в очередь. Использовать после DownloadAsync.
        /// </summary>
        /// <param name="segment"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">Если сегмента нет в списке. Всегда download должен быть первым, хз</exception>
        public void Queue(StreamSegment segment)
        {
            lock (locker)
            {
                QueueItem item = liveList.First(q => q.segment == segment);
                liveList.Remove(item);

                queue.Enqueue(item);

                if (processing)
                    return;

                processing = true;
            }

            _ = Task.Run(ProcessingLoopAsync);
        }

        async void ProcessingLoopAsync()
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

                await item.downloadOperationTask;

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
                foreach (var live in liveList)
                {
                    live.bufferWriteStream.Dispose();
                }
                liveList.Clear();

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