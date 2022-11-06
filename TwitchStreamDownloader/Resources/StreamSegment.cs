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

        public readonly string resolution;
        public readonly float framerate;

        public StreamSegment(Uri uri, string? title, int mediaSequenceNumber, float duration, DateTimeOffset programDate, string resolution, float framerate)
        {
            this.uri = uri;
            this.title = title;
            this.mediaSequenceNumber = mediaSequenceNumber;
            this.duration = duration;
            this.programDate = programDate;
            this.resolution = resolution;
            this.framerate = framerate;
        }

        public bool IsLive => string.Equals(title, "live", StringComparison.OrdinalIgnoreCase);
    }
}