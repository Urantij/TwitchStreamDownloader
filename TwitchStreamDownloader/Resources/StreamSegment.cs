using TwitchStreamDownloader.Download;

namespace TwitchStreamDownloader.Resources;

public class StreamSegment
{
    public Uri Uri { get; }
    public string? Title { get; }
    public int MediaSequenceNumber { get; }
    /// <summary>
    /// Секунды
    /// </summary>
    public float Duration { get; }
    public DateTimeOffset ProgramDate { get; }

    public Quality Quality { get; }

    public StreamSegment(Uri uri, string? title, int mediaSequenceNumber, float duration, DateTimeOffset programDate, Quality quality)
    {
        this.Uri = uri;
        this.Title = title;
        this.MediaSequenceNumber = mediaSequenceNumber;
        this.Duration = duration;
        this.ProgramDate = programDate;
        this.Quality = quality;
    }

    public bool IsLive() => string.Equals(Title, "live", StringComparison.OrdinalIgnoreCase);
}
