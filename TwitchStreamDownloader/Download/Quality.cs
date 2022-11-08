using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtM3UPlaylistParser.Models;
using TwitchStreamDownloader.Extensions;

namespace TwitchStreamDownloader.Download;

public class Quality
{
    public readonly Resolution? resolution;
    public readonly float fps;

    public Quality(Resolution? resolution, float fps)
    {
        this.resolution = resolution;
        this.fps = fps;
    }

    public bool Same(Quality quality)
    {
        if (quality.resolution != null)
            return this.resolution?.Same(quality.resolution) == true && this.fps == quality.fps;

        return this.resolution == null && this.fps == quality.fps;
    }
}
