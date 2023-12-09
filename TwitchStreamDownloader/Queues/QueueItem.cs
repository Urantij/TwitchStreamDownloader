using TwitchStreamDownloader.Resources;

namespace TwitchStreamDownloader.Queues;

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
