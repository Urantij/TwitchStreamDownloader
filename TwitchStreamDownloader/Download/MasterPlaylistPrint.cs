namespace TwitchStreamDownloader.Download
{
    /// <summary>
    /// Мастер плейлист даёт некоторую информацию о стриме. Хранить её было бы здорово.
    /// </summary>
    public class MasterPlaylistPrint
    {
        /// <summary>
        /// Когда время было взято. UTC
        /// </summary>
        public readonly DateTime addedDate;
        /// <summary>
        /// Само время
        /// </summary>
        public readonly TimeSpan streamTime;

        /// <param name="addedDate">UTC</param>
        public MasterPlaylistPrint(DateTime addedDate, float streamTime)
        {
            this.addedDate = addedDate;
            this.streamTime = TimeSpan.FromSeconds(streamTime);
        }

        /// <summary>
        /// UTC
        /// </summary>
        public DateTimeOffset GetEstimatedStreamStartTime()
        {
            return addedDate - streamTime;
        }

        public TimeSpan GetEstimatedStreamLength()
        {
            return DateTime.UtcNow - GetEstimatedStreamStartTime();
        }
    }
}