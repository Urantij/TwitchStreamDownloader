using System;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using TwitchStreamDownloader.Exceptions;
using TwitchStreamDownloader.Models;

namespace TwitchStreamDownloader.Net
{
    public class AccessToken
    {
        public readonly string value;
        public readonly AccessTokenValue parsedValue;
        public readonly string signature;

        public AccessToken(string value, AccessTokenValue parsedValue, string signature)
        {
            this.value = value;
            this.parsedValue = parsedValue;
            this.signature = signature;
        }
    }

    public static class GqlNet
    {
        /// <exception cref="BadCodeException">Если хттп код не саксес.</exception>
        /// <exception cref="Exception">Скорее всего, не удалось совершить запрос.</exception>
        /// <exception cref="WrongContentException">Если содержимое ответа не такое, какое хотелось бы.</exception>
        internal static async Task<AccessToken> GetAccessToken(HttpClient client, string channel, string clientId, string deviceId, string oauth, CancellationToken cancellationToken)
        {
            const string query = @"query(
                $login: String!
                $playerType: String!
                $disableHTTPS: Boolean!
            ) {
                streamPlaybackAccessToken(
                    channelName: $login
                    params: {
                        disableHTTPS: $disableHTTPS
                        playerType: $playerType
                        platform: ""web""
                        playerBackend: ""mediaplayer""
                    }
                ) {
                    value
                    signature
                }
            }";

            object variables = new
            {
                login = channel,
                playerType = "site",
                disableHTTPS = false,
            };

            var responseContent = await RequestGql(client, query, variables, clientId, deviceId, oauth, cancellationToken);

            try
            {
                var parsed = JsonConvert.DeserializeAnonymousType(responseContent, new
                {
                    data = new
                    {
                        streamPlaybackAccessToken = new
                        {
                            value = "",
                            signature = ""
                        }
                    }
                });

                //вроде оно просто кидает ошибку, если не может, ну да ладно
                if (parsed == null)
                    throw new Exception("null");

                //не может же выдать нулл
                var parsedTokenValue = JsonConvert.DeserializeObject<AccessTokenValue>(parsed.data.streamPlaybackAccessToken.value)!;

                return new AccessToken(parsed.data.streamPlaybackAccessToken.value, parsedTokenValue, parsed.data.streamPlaybackAccessToken.signature);
            }
            catch (Exception e)
            {
                throw new WrongContentException("GetAccessToken", responseContent, e);
            }
        }

        /// <exception cref="BadCodeException">Если хттп код не саксес.</exception>
        /// <exception cref="Exception">Скорее всего, не удалось совершить запрос.</exception>
        private static async Task<string> RequestGql(HttpClient client, string query, object variables, string clientId, string deviceId, string oauth, CancellationToken cancellationToken)
        {
            var requestBodyObj = new
            {
#pragma warning disable IDE0037
                query = query,
                variables = variables,
#pragma warning restore IDE0037
            };

            var requestBody = JsonConvert.SerializeObject(requestBodyObj);

            string? responseContent;
            using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://gql.twitch.tv/gql"))
            {
                requestMessage.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                FormGqlRequestHeaders(requestMessage.Headers, clientId, deviceId, oauth);

                using var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead, cancellationToken);
                responseContent = await response.Content.ReadAsStringAsync(CancellationToken.None);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new BadCodeException(response.StatusCode, responseContent);
                }
            }

            return responseContent;
        }

        private static void FormGqlRequestHeaders(System.Net.Http.Headers.HttpRequestHeaders headers, string clientId, string deviceId, string oauth)
        {
            headers.Add("Accept-Language", "en-US");
            headers.Add("Authorization", oauth);

            headers.Add("Client-ID", clientId);
            headers.Add("X-Device-ID", deviceId);

            headers.Add("Referer", "https://www.twitch.tv");
            headers.Add("Origin", "https://www.twitch.tv");
        }

        public static string GenerateDeviceId(Random random)
        {
            //TODO затестить 1546549840000000
            return string.Concat("0000000000000000", random.NextDouble().ToString("N16").AsSpan(2));
        }
    }
}