using System.Net;
using System.Text;
using Newtonsoft.Json;
using TwitchStreamDownloader.Exceptions;
using TwitchStreamDownloader.Models;

namespace TwitchStreamDownloader.Net
{
    public class AccessTokenFields
    {
        public readonly string token;
        public readonly string signature;

        public AccessTokenFields(string token, string signature)
        {
            this.token = token;
            this.signature = signature;
        }
    }

    internal static class AccessTokenNet
    {
        /// <exception cref="BadCodeException">Если хттп код не саксес.</exception>
        /// <exception cref="WrongContentException">Если содержимое ответа не такое, какое хотелось бы.</exception>
        /// <exception cref="Exception">Скорее всего, не удалось совершить запрос.</exception>
        internal static async Task<AccessTokenFields> GetAccessToken(HttpClient client, string hash, string channel, string oauth, string clientId, CancellationToken cancellationToken)
        {
            var requestToken = new PlaybackAccessTokenRequestBody(hash, channel);

            var requestBody = JsonConvert.SerializeObject(requestToken);

            string? responseContent;
            using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://gql.twitch.tv/gql"))
            {
                requestMessage.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                requestMessage.Headers.Add("Accept", "*/*");
                requestMessage.Headers.Add("Authorization", oauth);
                requestMessage.Headers.Add("Accept-Language", "en-US");
                requestMessage.Headers.Add("Client-ID", clientId);

                requestMessage.Headers.Add("Origin", "https://www.twitch.tv");
                requestMessage.Headers.Add("Sec-Fetch-Site", "same-site");
                requestMessage.Headers.Add("Sec-Fetch-Mode", "cors");
                requestMessage.Headers.Add("Sec-Fetch-Dest", "empty");
                requestMessage.Headers.Add("Referer", "https://www.twitch.tv");
                requestMessage.Headers.Add("Accept-Encoding", "gzip, deflate, br");

                using var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead, cancellationToken);
                responseContent = await response.Content.ReadAsStringAsync(CancellationToken.None);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new BadCodeException(response.StatusCode, responseContent);
                }
            }

            try
            {
                var deserialized = JsonConvert.DeserializeObject<PlaybackAccessTokenResponseBody>(responseContent)!;

                return new AccessTokenFields(deserialized.data.streamPlaybackAccessToken.value, deserialized.data.streamPlaybackAccessToken.signature);
            }
            catch (Exception e)
            {
                throw new WrongContentException("GetAccessToken", responseContent, e);
            }
        }
    }
}