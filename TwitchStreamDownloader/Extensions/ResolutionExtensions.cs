using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtM3UPlaylistParser.Models;

namespace TwitchStreamDownloader.Extensions;

public static class ResolutionExtensions
{
    // Забыл это сделать в самом extm3u пакете, и ради этого переделывать впадлу.
    public static bool Same(this Resolution thisResolution, Resolution resolution)
    {
        return thisResolution.width == resolution.width && thisResolution.height == resolution.height;
    }
}
