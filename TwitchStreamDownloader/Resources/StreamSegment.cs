using TwitchStreamDownloader.Download;

namespace TwitchStreamDownloader.Resources
{
    public class StreamSegment
    {
        public readonly Uri uri;
        public readonly string? title;
        public readonly int mediaSequenceNumber;
        /// <summary>
        /// Секунды
        /// </summary>
        public readonly float duration;
        public readonly DateTimeOffset programDate;

        public readonly Quality quality;

        public StreamSegment(Uri uri, string? title, int mediaSequenceNumber, float duration, DateTimeOffset programDate, Quality quality)
        {
            this.uri = uri;
            this.title = title;
            this.mediaSequenceNumber = mediaSequenceNumber;
            this.duration = duration;
            this.programDate = programDate;
            this.quality = quality;
        }

        public bool IsLive => string.Equals(title, "live", StringComparison.OrdinalIgnoreCase);
    }
}