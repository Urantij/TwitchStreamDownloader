using TwitchStreamDownloader.Download;
using TwitchStreamDownloader.Resources;

namespace TwitchStreamDownloader.Queues
{
    /// <summary>
    /// Нужно юзать этот объект в локе, когда стреляешь хендлеры. чтобы соблюсти порядок
    /// </summary>
    public class SegmentsGroup
    {
        /// <summary>
        /// Не нулл, нужен, чтобы искать остальные сегменты по намберу.
        /// </summary>
        public readonly StreamSegment first;

        /// <summary>
        /// Не нулл, если найден сегмент без рекламы. он будет тут.
        /// </summary>
        public StreamSegment? live;

        /// <summary>
        /// Сегмент может сидеть и ждать в очереди, если мы ждём, когда придёт его лайв или когда придут все даунлодеры, чтобы не ждать.
        /// Ещё можем ждать, если был пропущен сегмент, но я условился, что пропущенных сегментов почти не может быть, так что на них похуй.
        /// Короче. Эта хуйня отменяется, если не нужно ждать в очереди. Значит, если пришёл лайв или вся хуйня, то отмена. Всио.
        /// </summary>
        public readonly CancellationTokenSource sleepSource = new();

        /// <summary>
        /// Следит, от каких загрузчиков пришли сегменты.
        /// </summary>
        public readonly List<SegmentsDownloader> sources = new();
        /// <summary>
        /// Какое качество сегмента.
        /// </summary>
        public readonly string quality;

        /// <summary>
        /// UTC
        /// </summary>
        public readonly DateTime addedDate;

        public SegmentsGroup(StreamSegment first, string quality, DateTime addedDate)
        {
            this.first = first;
            this.quality = quality;
            this.addedDate = addedDate;
        }
    }

    public class SegmentsGroupEventArgs : EventArgs
    {
        public SegmentsGroup SegmentsGroup { get; set; }
        //TODO сделать, чтобы загрузчик выдавался не первый в мире, а лайв по возможности (в лайве он и так лайвовый, а в очереди нет)
        public SegmentsDownloader SegmentsDownloader { get; set; }

        public SegmentsGroupEventArgs(SegmentsGroup segmentsGroup, SegmentsDownloader segmentsDownloader)
        {
            SegmentsGroup = segmentsGroup;
            SegmentsDownloader = segmentsDownloader;
        }
    }

    /* TODO смысл в том, что медиа секвенс намберы оказались уникальными для каждого загрузчика
     * Значит, нужно их синхронизировать по времени
     * А мне впадлу пока думать, так что потом. */

    [Obsolete("Хз, какой атрибут поставить. короче, не юзабельно")]
    /// <summary>
    /// Пытается юзать несколько загрузчиков вместе и синхронизировать их сегменты.
    /// Типа один загрузчик поймал рекламу, а другой нет.
    /// </summary>
    public class MultiDownloaderQueue : IDisposable
    {
        /* Значит. Когда приходит инфа о сегменте, смотрим, является ли он для нас новым
         * Если он младше последнего записанного сегмента, дискард
         * Если он следующий после последнего записанного, пишем его и делаем последним записанным.
         * // Сегмент вообще будущим быть может?
         * Если он будущий, то ставим его в очередь и ждём, что, может быть, появится сегмент между ними (Пропущенный)
         * Ждём пропущенный сегмент секунд 5 (хотя по факту нужно 5 секунд или обновление всех плейлистов)
         * Если за 5 секунд не пришёл, пишем последний доступный. */

        /* При этом сегмент может быть рекламным. То есть он проебал контент
         * И этот контент может быть в другой загрузчике.
         * А может и не быть.
         * Значит, когда приходит рекламный сегмент, мы его сохраняем в очереди
         * И если за 5 секунд не пришёл оригинальный откуда-нибудь, или пришли со всех загрузчиков и оригинального не было, дискардим и считать записанным
         * Если пришёл оригинальный, качаем и пишем */

        /* Но прежде чем писать, сегмент нужно СКАЧАТЬ.
         * Значит, если приходит следующий или будущий и нерекламный сегмент, качаем его.
         * Но качаем один раз, хоть загрузчиков и несколько
         * При этом, если не даст скачать, или будет качаться дольше позволенного (секунды 3?) то дискард и считать записанным */

        /* короче. 2 хендлера тогда
         * первый хендлер выдаёт все сегменты по порядку с учётом таймаутов.
         * он выкидывает следующий доступный сегмент, ждёт, пока придёт пропущенный, ждёт, пока все загрузчики отчитаются, что рекламный пропущен.
         * то есть вызывается, когда хуйня выкидыавется из очереди потому что оно "обработалось"
         * он нужен, чтобы получатель мог по порядку записывать сегменты на диск
         * второй хендлер выкидывает все уникальные сегменты без рекламы, как они приходят
         * то есть приходит сегмент, похуй, встал он в очередь или сразу записался, если нет рекламы -> кидаем
         * он нужен, чтобы получатель мог заранее начать качать сегменты. */

        public bool Disposed { get; private set; } = false;

        private readonly object flowLocker = new ();

        /// <summary>
        /// Если плейлист обновился последний такое то количество времени назад или больше, то нам как то не очень интересно, что он там думает
        /// TODO Добавить бы, чтобы это было время от медиаплейлист апдейт тайм самого грузителя, чтобы не было тупой хуйни, но впадлу
        /// </summary>
        private readonly TimeSpan playlistMaxMediaLastUpdateTime = TimeSpan.FromSeconds(5);
        private readonly TimeSpan missingSegmentWaitTime = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Должен быть защищён флоулокером
        /// </summary>
        private readonly List<SegmentsDownloader> downloaders = new ();

        /// <summary>
        /// Должен быть защищён флоулокером
        /// </summary>
        private bool segmentsProcessing = false;

        /// <summary>
        /// Должен быть защищён флоулокером
        /// </summary>
        private readonly List<SegmentsGroup> queueSegments = new ();
        /// <summary>
        /// Если новый сегмент имеет намбер меньше и время меньше, то он нам не нужен.
        /// Должен быть защищён флоулокером
        /// </summary>
        private StreamSegment lastSegment;

        //смысла в этих пропсах нет, ради прекола
        //public SegmentsGroupHandler QueuedGroupHandler { private get; set; }
        //public SegmentsGroupHandler LiveGroupHandler { private get; set; }
        public EventHandler<SegmentsGroupEventArgs> QueuedGroupHandler { private get; set; }
        public EventHandler<SegmentsGroupEventArgs> LiveGroupHandler { private get; set; }

        public EventHandler<string> LogHandler { private get; set; }
        public EventHandler<string> DebugHandler { private get; set; }
        public EventHandler<string> CriticalHandler { private get; set; }

        public MultiDownloaderQueue(TimeSpan missingSegmentWaitTime)
        {
            this.missingSegmentWaitTime = missingSegmentWaitTime;
        }

        /// <summary>
        /// Просто добавляет и подписывает его хендлеры. Не запускает.
        /// Юзает SegmentArrivedHandler и MediaPlaylistProcessedHandler
        /// </summary>
        /// <param name="downloader"></param>
        public void AddDownloader(SegmentsDownloader downloader)
        {
            lock (flowLocker)
            {
                downloaders.Add(downloader);

                downloader.SegmentArrived += (sender, s) => SegmentArrived(s, downloader);
                downloader.MediaPlaylistProcessed += (sender, args) => MediaPlaylistProcessed(downloader);
            }
        }

        /// <summary>
        /// Просто убирает хендлеры и сам загрузчик. Не диспоузит
        /// </summary>
        /// <param name="downloader"></param>
        public void RemoveDownloader(SegmentsDownloader downloader)
        {
            lock (flowLocker)
            {
                throw new Exception("не убрал");
                //downloader.SegmentArrivedHandler = null;
                //downloader.MediaPlaylistProcessedHandler = null;

                downloaders.Remove(downloader);
            }
        }

        private int CountValidDownloaders()
        {
            lock (flowLocker)
            {
                return downloaders.Where(d =>
                {
                    if (d.LastMediaPlaylistUpdate == null)
                        return false;

                    var passed = DateTime.UtcNow - d.LastMediaPlaylistUpdate.Value;

                    return passed <= playlistMaxMediaLastUpdateTime;
                }).Count();
            }
        }

        /// <summary>
        /// Если тру, first сегмент находится на таймлайне после second (если ты не понял)
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        private static bool IsFirstAfterSecond(StreamSegment first, StreamSegment second)
        {
            return first.mediaSequenceNumber > second.mediaSequenceNumber ||
                   first.programDate > second.programDate;
        }

        /// <summary>
        /// Если тру, first прямо следующий после second
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        private static bool IsFirstRightAfterSecond(StreamSegment first, StreamSegment second)
        {
            return first.mediaSequenceNumber == second.mediaSequenceNumber + 1 ||
                   first.programDate > second.programDate;
        }

        #region Unused
        private async void StartProcessQueue()
        {
            /* В теории можно попасть в ситуацию, когда этот метод заканчивается, а новый метод стопится
             * Но это супер маловероятно
             * И может навредить, только если застопится последний сегмент плейлисты
             * Что супер дупер маловероятно (суммарно)
             * Но TODO исправить было бы неплохо, хотя бы чтобы я спал спокойной */

            lock (flowLocker)
            {
                if (segmentsProcessing)
                    return;

                segmentsProcessing = true;
            }

            try
            {
                await ProcessQueueAsync();
            }
            catch (Exception e)
            {
                OnCritical($"Process Loop Exception:\n{e}");
            }

            lock (flowLocker)
            {
                segmentsProcessing = false;
            }
        }

        private async Task ProcessQueueAsync()
        {
            OnDebug("Processing...");

            while (!Disposed)
            {
                SegmentsGroup firstQ;
                int sourcesCount;
                lock (flowLocker)
                {
                    firstQ = queueSegments.FirstOrDefault();

                    if (firstQ != null)
                    {
                        sourcesCount = CountValidDownloaders();
                    }
                    else return;
                }

                bool? raise = null;
                TimeSpan? waitTime = null;

                if (firstQ.live == null)
                {
                    bool allSources;
                    lock (firstQ)
                    {
                        //в тевории может быть больше
                        allSources = firstQ.sources.Count >= sourcesCount;
                    }

                    if (allSources)
                    {
                        raise = true;
                    }
                    else
                    {
                        var passed = DateTime.UtcNow - firstQ.addedDate;

                        if (passed > missingSegmentWaitTime)
                        {
                            OnDebug($"Couldnt find live of {firstQ.first.mediaSequenceNumber}");

                            raise = true;
                        }
                        else
                        {
                            waitTime = missingSegmentWaitTime - passed;
                            raise = false;
                        }
                    }
                }

                //если мы не уверены, бежим или стреляем, значит, нужно решать дальше
                if (raise == null)
                {
                    if (lastSegment != null)
                    {
                        //TODO возможно, если мы судим по времени, нужно ждать мисс сегмент тайм, потому что мы не 100% уверены, что там не было ещё
                        if (firstQ.first.mediaSequenceNumber == lastSegment.mediaSequenceNumber + 1 ||
                            firstQ.first.programDate > lastSegment.programDate)
                        {
                            OnDebug($"Find live of {firstQ.first.mediaSequenceNumber} ({firstQ.first.mediaSequenceNumber} in right sequence!)");

                            raise = true;
                        }
                        else
                        {
                            var passed = DateTime.UtcNow - firstQ.addedDate;

                            if (passed > missingSegmentWaitTime)
                            {
                                OnDebug($"Find live of {firstQ.first.mediaSequenceNumber} ({lastSegment.mediaSequenceNumber}->{firstQ.first.mediaSequenceNumber}...)");

                                raise = true;
                            }
                            else
                            {
                                waitTime = missingSegmentWaitTime - passed;
                                raise = false;
                            }
                        }
                    }
                    else
                    {
                        OnDebug($"Find live of {firstQ.first.mediaSequenceNumber} (First!)");

                        raise = true;
                    }
                }

                //ну и исходы
                if (raise == null)
                {
                    OnCritical($"Send == null");

                    lock (flowLocker)
                    {
                        queueSegments.RemoveAt(0);
                        firstQ.sleepSource.Dispose();
                    }
                }
                else if (raise == false)
                {
                    if (waitTime != null)
                    {
                        try
                        {
                            await Task.Delay(waitTime.Value * 1.1, firstQ.sleepSource.Token);
                        }
                        catch { }

                        lock (flowLocker)
                        {
                            queueSegments.RemoveAt(0);
                            firstQ.sleepSource.Dispose();

                            lastSegment = firstQ.first;
                        }

                        SegmentsDownloader downloader;
                        lock (firstQ)
                        {
                            downloader = firstQ.sources[0];
                        }

                        QueuedGroupHandler?.Invoke(this, new SegmentsGroupEventArgs(firstQ, downloader));
                    }
                    else
                    {
                        OnCritical($"raise == false && waitTime == null");

                        lock (flowLocker)
                        {
                            queueSegments.RemoveAt(0);
                            firstQ.sleepSource.Dispose();
                        }
                    }
                }
                else
                {
                    lock (flowLocker)
                    {
                        queueSegments.RemoveAt(0);
                        firstQ.sleepSource.Dispose();

                        lastSegment = firstQ.first;
                    }

                    SegmentsDownloader downloader;
                    lock (firstQ)
                    {
                        downloader = firstQ.sources[0];
                    }

                    QueuedGroupHandler?.Invoke(this, new SegmentsGroupEventArgs(firstQ, downloader));
                }
            }
        }
        #endregion

        private async void SegmentArrived(StreamSegment s, SegmentsDownloader downloader)
        {
            /* тут такое дело
             * Лайв хендлер отрабатывает после лока
             * В теории, если в данный момент этот же сегмент находится в процессе
             * Он может встать в очередь на лок
             * И когда тут хуйня выйдет, он сразу же зайдёт и выстрелил queue (типа таймаут)
             * А потом тут выстрелит лайв версия
             * В тевории, можно делать лок на сам сегмент ещё в флоу локе, и следить за двумя локами
             * Или как-то сделать лок на сегмент, когда происходит отмена. Если в локе написано, что отмена, значит отмена
             * Хз. Тогда нужно следить, чтобы тред с отменой не влетел в лок говна. */

            //если мы раньше его лайв не видели, стоит выкинуть
            bool raiseLive = false;

            //
            bool raiseCancellation = false;

            SegmentsGroup segmentsGroup;
            lock (flowLocker)
            {
                if (lastSegment != null && IsFirstAfterSecond(lastSegment, s))
                    return;

                //Если будут частые переподрубы и начнут смешиваться плейлисты с намербами от 0, то похуй?
                //как только хоть один из плейлистов дойдёт до 10+ сегмента, то всё будет норм.
                segmentsGroup = queueSegments.FirstOrDefault(q => q.first.mediaSequenceNumber == s.mediaSequenceNumber);

                //группируем
                if (segmentsGroup == null)
                {
                    segmentsGroup = new SegmentsGroup(s, downloader.LastVideo, DateTime.UtcNow);

                    //queueSegments = queueSegments.OrderBy(q => q.playlistPrintNumber).ThenBy(q => q.first.mediaSequenceNumber).ToList();

                    bool inserted = false;
                    for (int index = queueSegments.Count - 1; index >= 0; index--)
                    {
                        var item = queueSegments[index];

                        if (segmentsGroup.first.mediaSequenceNumber > item.first.mediaSequenceNumber ||
                            segmentsGroup.first.programDate > item.first.programDate)
                        {
                            queueSegments.Insert(index + 1, segmentsGroup);
                            inserted = true;
                        }
                    }

                    if (!inserted)
                    {
                        queueSegments.Insert(0, segmentsGroup);
                    }

                    OnDebug($"Created Grouped {segmentsGroup.first.mediaSequenceNumber}");
                }

                if (segmentsGroup.live == null && s.IsLive)
                {
                    segmentsGroup.live = s;
                    raiseLive = true;
                    raiseCancellation = true;
                }

                int validDownloaders = CountValidDownloaders();
                int sourcesCount; //даже инвалидные бтв. TODO учитывать только валидные и сурсы в сегменте
                lock (segmentsGroup)
                {
                    //в тевории, если этот сегмент уже был, то считать ничего нет смысла. так как не было изменений
                    if (!segmentsGroup.sources.Contains(downloader))
                    {
                        segmentsGroup.sources.Add(downloader);
                    }

                    sourcesCount = segmentsGroup.sources.Count;
                }

                //мы ждём, если нет лайва и ждём валидных
                if (sourcesCount >= validDownloaders)
                {
                    raiseCancellation = true;
                }
            }

            if (raiseLive)
            {
                /* Хуйня отменяется в очереди в локе, и тут проверяется в локе
                 * Значит, либо она успела отменится, и мы ничего тут не отправим
                 * Либо она отменится там только после того, как мы тут отправим.
                 * И порядок будет соблюдён */
                lock (segmentsGroup)
                {
                    if (!segmentsGroup.sleepSource.IsCancellationRequested)
                    {
                        OnDebug($"Firing live {segmentsGroup.first.mediaSequenceNumber}");
                        try
                        {
                            LiveGroupHandler?.Invoke(this, new SegmentsGroupEventArgs(segmentsGroup, downloader));
                        }
                        catch (Exception e)
                        {
                            OnLog($"Fire Live Exception\n{e}");
                        }
                    }
                    else
                    {
                        raiseCancellation = false;
                    }
                }
            }

            if (raiseCancellation)
            {
                /* Если хуйня находится в слипе с этим токеном, то в случае отмены оно там и продолжится
                 * И оно там заходит в локи, где первый лок это flow
                 * И всегда нужно заходить сначала flow, а потом segment
                 * А если не делать тут flow лок, то залочит segment -> flow -> segment
                 * И это неправильный порядок.
                 * С другой стороны, блять, тогда всё отправление будет сделано через флоу лок, что тоже кал.
                 * Короче. просто уберу лок тут. Что самое худшее произойдёт? Тут будет ошибка, если после диспоуза случилось. */

                try
                {
                    segmentsGroup.sleepSource.Cancel();
                }
                catch
                {
                    /* Честно говоря, что это значит, я не знаю
                     * То есть, за то временное окно, которое случилось между локами этим и флоулоком или лайвлоком успела пройти хуйня в очереди
                     * и задиспоузилась
                     * а?
                     * похуй. */
                }

                //не делают ретурн, потому что похуй в целом. такая редкая ситуация, лишний заход в цикл нихуя не зароляет
            }

            try
            {
                await Process2Async();
            }
            catch (Exception e)
            {
                OnCritical($"Process2 Exception\n{e}");
            }
        }

        private async Task Process2Async()
        {
            lock (flowLocker)
            {
                if (segmentsProcessing)
                    return;

                segmentsProcessing = true;
            }

            OnDebug("Processing...");

            while (!Disposed)
            {
                SegmentsGroup firstQ;
                lock (flowLocker)
                {
                    firstQ = queueSegments.FirstOrDefault();

                    if (firstQ == null)
                    {
                        segmentsProcessing = false;
                        return;
                    }
                }

                //Если не получили жизненную жижу, подождём мб.
                if (firstQ.live == null)
                {
                    int validSourcesCount = CountValidDownloaders();

                    bool allSources;
                    lock (firstQ)
                    {
                        //в тевории может быть больше
                        allSources = firstQ.sources.Count >= validSourcesCount;
                    }

                    if (!allSources)
                    {
                        bool durka;

                        var passed = DateTime.UtcNow - firstQ.addedDate;

                        if (passed < missingSegmentWaitTime)
                        {
                            var waitTime = missingSegmentWaitTime - passed;

                            try
                            {
                                await Task.Delay(waitTime * 1.1, firstQ.sleepSource.Token);
                            }
                            catch { }

                            lock (firstQ)
                            {
                                durka = firstQ.live == null;
                            }
                        }
                        else
                        {
                            durka = true;
                        }

                        if (durka)
                        {
                            OnDebug($"Couldnt find live of {firstQ.first.mediaSequenceNumber}");
                        }
                    }
                }

                //Отправляем
                lock (flowLocker)
                {
                    lock (firstQ)
                    {
                        //если бы можно было проверять флаг диспоуз, это было бы не нужно
                        try { firstQ.sleepSource.Cancel(); }
                        catch { }

                        firstQ.sleepSource.Dispose();
                    }

                    queueSegments.RemoveAt(0);
                    lastSegment = firstQ.first;
                }

                /* Значит, после того, как мы отменили слипсурс, а также сделали этот сегмент последним записанным
                 * Снова приходя, этот сегмент будет резаться о медианамбер
                 * А уже пришедший сегмент проверит слипсурс и ливнет с позором */

                SegmentsDownloader downloader;
                lock (firstQ)
                {
                    downloader = firstQ.sources[0];
                }

                try
                {
                    QueuedGroupHandler?.Invoke(this, new SegmentsGroupEventArgs(firstQ, downloader));
                    //QueuedGroupHandler?.Invoke(segmentsGroup, downloader);
                }
                catch (Exception e)
                {
                    OnCritical($"SendNahui Exception\n{e}");
                }
            }

            //после диспоуза тааак важно это сделать, ёбнешься.
            lock (flowLocker)
            {
                segmentsProcessing = false;
            }
        }

        private void MediaPlaylistProcessed(SegmentsDownloader downloader)
        {
            //throw new NotImplementedException();
        }

        private void OnCritical(string str)
        {
            CriticalHandler?.Invoke(this, str);
        }

        private void OnDebug(string str)
        {
            DebugHandler?.Invoke(this, str);
        }

        private void OnLog(string str)
        {
            LogHandler?.Invoke(this, str);
        }

        public void Dispose()
        {
            if (Disposed)
                return;

            Disposed = true;

            lock (flowLocker)
            {
                foreach (var downloader in downloaders)
                {
                    downloader.Dispose();
                }

                downloaders.Clear();
                queueSegments.Clear();
            }
        }
    }
}