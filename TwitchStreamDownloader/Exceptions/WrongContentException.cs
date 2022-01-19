namespace TwitchStreamDownloader.Exceptions
{
    public class WrongContentException : Exception
    {
        public readonly string place;
        public readonly string content;

        public WrongContentException(string place, string content, Exception exception)
            : base("Content wasnt parsed properly", exception)
        {
            this.place = place;
            this.content = content;
        }
    }
}