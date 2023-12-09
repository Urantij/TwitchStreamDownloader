using System.Net;

namespace TwitchStreamDownloader.Exceptions;

/// <summary>
/// Если http запрос выдал не саксес
/// </summary>
public class BadCodeException : Exception
{
    public readonly HttpStatusCode statusCode;
    public readonly string responseContent;

    public BadCodeException(HttpStatusCode statusCode, string responseContent) 
        : base($"Bad Code ({statusCode})")
    {
        this.statusCode = statusCode;
        this.responseContent = responseContent;
    }
}
