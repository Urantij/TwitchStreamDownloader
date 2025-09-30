using System.Net;

namespace TwitchStreamDownloader.Exceptions;

/// <summary>
/// Если http запрос выдал не саксес
/// </summary>
public class BadCodeException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string ResponseContent { get; }

    public BadCodeException(HttpStatusCode statusCode, string responseContent)
        : base($"Bad Code ({statusCode})")
    {
        this.StatusCode = statusCode;
        this.ResponseContent = responseContent;
    }
}