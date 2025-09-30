namespace TwitchStreamDownloader.Download;

/// <summary>
/// Мастер плейлист даёт некоторую информацию о стриме. Хранить её было бы здорово.
/// </summary>
public class MasterPlaylistPrint
{
    /// <summary>
    /// Когда время было взято. UTC
    /// </summary>
    public DateTime AddedDate { get; }

    /// <summary>
    /// Само время
    /// </summary>
    public TimeSpan StreamTime { get; }

    public MasterPlaylistPrint(DateTime addedDate, float streamTime)
    {
        this.AddedDate = addedDate;
        this.StreamTime = TimeSpan.FromSeconds(streamTime);
    }

    /// <summary>
    /// UTC
    /// </summary>
    public DateTime GetEstimatedStreamStartTime()
    {
        return AddedDate - StreamTime;
    }

    public TimeSpan GetEstimatedStreamLength()
    {
        return DateTime.UtcNow - GetEstimatedStreamStartTime();
    }
}