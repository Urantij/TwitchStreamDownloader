using System.Text.Json.Serialization;

namespace TwitchStreamDownloader.Models;

internal class PlaybackAccessTokenResponseBody(PlaybackAccessTokenResponseBody.TokenData data)
{
    public class TokenData(TokenData.StreamPlaybackAccessTokenData streamPlaybackAccessToken)
    {
        public class StreamPlaybackAccessTokenData(string value, string signature)
        {
            [JsonPropertyName("value")] public string Value { get; } = value;

            [JsonPropertyName("signature")] public string Signature { get; } = signature;
        }

        [JsonPropertyName("streamPlaybackAccessToken")]
        public StreamPlaybackAccessTokenData StreamPlaybackAccessToken { get; } = streamPlaybackAccessToken;
    }

    [JsonPropertyName("data")] public TokenData Data { get; } = data;
}