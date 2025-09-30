using System.Collections.Specialized;
using System.Web;

namespace TwitchStreamDownloader.Net;

public static class UsherNet
{
    internal static Uri CreateUsherUri(string channel, string sig, string token, bool fastBread, string playSessionId,
        Random random)
    {
        NameValueCollection nameValue = HttpUtility.ParseQueryString("");
        nameValue["allow_source"] = true.ToString();
        nameValue["allow_audio_only"] = true.ToString();
        nameValue["cdm"] = "wv";
        nameValue["fast_bread"] = fastBread.ToString();
        nameValue["player_backend"] = "mediaplayer";
        nameValue["playlist_include_framerate"] = true.ToString();
        nameValue["reassignments_supported"] = true.ToString();
        nameValue["p"] = ((int)(random.NextDouble() * 9999999)).ToString();

        nameValue["token"] = token;
        nameValue["sig"] = sig;

        nameValue["play_session_id"] = playSessionId;

        return new Uri($"https://usher.ttvnw.net/api/channel/hls/{channel}.m3u8?{nameValue}");
    }

    internal static Uri CreateUsherUri_old(string channel, string sig, string token, bool fastBread, int p)
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

    public static string GenerateUniqueId(Random random)
    {
        const string allowed = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        char[] chars = new char[32];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = allowed[random.Next(0, allowed.Length)];
        }

        return new string(chars);
    }
}