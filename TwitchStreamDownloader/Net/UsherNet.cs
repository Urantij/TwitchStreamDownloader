using System.Web;
using System.Collections.Specialized;

namespace TwitchStreamDownloader.Net
{
    internal static class UsherNet
    {
        internal static Uri CreateUsherUri(string channel, string sig, string token, bool fastBread, int p)
        {
            string endPoint = $"/api/channel/hls/{channel}.m3u8";

            NameValueCollection nameValue = HttpUtility.ParseQueryString("");
            nameValue["allow_source"] = true.ToString();
            nameValue["dt"] = 2.ToString();
            nameValue["fast_bread"] = fastBread.ToString();
            nameValue["p"] = p.ToString();
            nameValue["player_backend"] = "mediaplayer";
            nameValue["playlist_include_framerate"] = true.ToString();
            nameValue["sig"] = sig;
            nameValue["token"] = token;
            nameValue["cdm"] = "wv";
            nameValue["player_version"] = "1.4.0";

            string url = $"https://usher.ttvnw.net{endPoint}?{nameValue}";

            return new Uri(url);
        }
    }
}