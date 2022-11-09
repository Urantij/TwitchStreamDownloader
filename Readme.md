# TwitchStreamDownloader
Качает стримы с твича.

### Как юзать

```c#

var segmentsDownloader = new SegmentsDownloader(httpClient, settings, channel);
var downloadQueue = new DownloadQueue(TimeSpan.FromSeconds(10));

// Пришёл конец плейлиста - стрим закончился.
segmentsDownloader.PlaylistEnded += (object? sender, EventArgs e)
{
    segmentsDownloader.Stop();
};

// Пришла информация о сегменте.
segmentsDownloader.SegmentArrived += async (object? sender, StreamSegment segment) =>
{
    // Если он не лайв, значит он рекламный.
    if (!segment.IsLive)
        return;

    // Кладём в очередь.
    QueueItem queueItem = downloadQueue.Queue(segment, new MemoryStream());

    try
    {
        // Грузим.
        await downloadQueue.DownloadAsync(httpClient, queueItem);
    }
    catch (Exception e)
    {
        // Обработать тут можно, если хочется.
    }
};

// Сегмент либо скачался, либо не скачался.
downloadQueue.ItemDequeued += async (object? sender, QueueItem qItem) =>
{
    try
    {
        // Если сегмент был записан в буфер...
        if (qItem.Written)
        {
            // Ето важно
            qItem.bufferWriteStream.Position = 0;

            // В qItem.bufferWriteStream лежит сегмент, тут мона его перенаправить в файлстрим, например
        }
    }
    finally
    {
        await qItem.bufferWriteStream.DisposeAsync();
    }
};

segmentsDownloader.Start();

```

### А в чём смысл?

TwitchSegmentsDownloader качает и парсит плейлист трансляции, то есть мета инфу о видео сегментах трансляции.
После чего DownloadQueue уже качает само видео. Но загружает сегменты как получится, поэтому качает в буферный стрим, из которых уже нужно передавать стрим куда надо.

### Нюансы

SegmentsDownloader будет пытаться качать трансляцию до упора. То есть даже если её уже нет, он будет пытаться достать из неё плейлист, пока не будет вызван Stop.

### Почему так переусложнено?

Потому что раньше предполагалось, что загрузчиков сегментов может быть несколько. Но потом я передумал, а переделывать, ну, как-то не захотелось.