using Newtonsoft.Json;

#nullable disable warnings
namespace TwitchStreamDownloader.Models;

internal class PlaybackAccessTokenResponseBody
{
    public class Data
    {
        public class StreamPlaybackAccessToken
        {
            [JsonRequired]
            public string value;
            [JsonRequired]
            public string signature;
        }

        [JsonRequired]
        public StreamPlaybackAccessToken streamPlaybackAccessToken;
    }

    [JsonRequired]
    public Data data;
}
#nullable restore warnings
