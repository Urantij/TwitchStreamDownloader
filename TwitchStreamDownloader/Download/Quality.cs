using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtM3UPlaylistParser.Models;

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
}
