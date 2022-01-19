namespace TwitchStreamDownloader.Exceptions
{
    /// <summary>
    /// Кидается, если приказано выбрать определённое качество, а его нет
    /// </summary>
    public class NoQualityException : Exception
    {
        public string[] options;

        public NoQualityException(string[] options)
            : base($"Cant find quality. Options: {string.Join(',', options)}")
        {
            this.options = options;
        }
    }
}