namespace TwitchStreamDownloader.Exceptions;

/// <summary>
/// Если не удалось пропарсить
/// </summary>
public class WrongContentException : Exception
{
    public string Place { get; }
    public string Content { get; }

    public WrongContentException(string place, string content, Exception? exception)
        : base($"Content wasnt parsed properly. ({place})\n{content}", exception)
    {
        this.Place = place;
        this.Content = content;
    }
}