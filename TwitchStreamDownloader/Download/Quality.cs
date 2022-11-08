using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtM3UPlaylistParser.Models;
using TwitchStreamDownloader.Extensions;

namespace TwitchStreamDownloader.Download;

public class Quality
{
    public readonly Resolution resolution;
    public readonly float fps;

    public Quality(Resolution resolution, float fps)
    {
        this.resolution = resolution;
        this.fps = fps;
    }

    public bool Same(Quality quality)
    {
        return this.resolution.Same(quality.resolution) && this.fps == quality.fps;
    }
}
