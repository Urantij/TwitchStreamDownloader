using System.Net;

namespace TwitchStreamDownloader.Exceptions
{
    public class BadCodeException : Exception
    {
        public readonly HttpStatusCode statusCode;
        public readonly string responseContent;

        public BadCodeException(HttpStatusCode statusCode, string responseContent) 
        {
            this.statusCode = statusCode;
            this.responseContent = responseContent;
        }
    }
}