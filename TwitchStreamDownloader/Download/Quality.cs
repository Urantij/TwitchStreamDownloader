using ExtM3UPlaylistParser.Models;
using TwitchStreamDownloader.Extensions;

namespace TwitchStreamDownloader.Download;

public class Quality
{
    public Resolution? Resolution { get; }
    public float Fps { get; }

    public Quality(Resolution? resolution, float fps)
    {
        this.Resolution = resolution;
        this.Fps = fps;
    }

    public bool Same(Quality quality)
    {
        if (quality.Resolution != null)
            return this.Resolution?.Same(quality.Resolution) == true && this.Fps == quality.Fps;

        return this.Resolution == null && this.Fps == quality.Fps;
    }
}