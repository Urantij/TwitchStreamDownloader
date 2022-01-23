using System.Net;

namespace TwitchStreamDownloader.Download
{
    public class SegmentsDownloaderSettings
    {
        //Если есть качество, возьмёт это качество. По возможности также возьмёт фпс.
        //Если есть фпс, но нет качествао, то па е бать ыхых
        //TODO если нулл, должен брать сурс, но скорее всего будет брать срань
        public string? preferredQuality = null;
        public string? preferredFps = null;

        /// <summary>
        /// Только для качества, не фпс. Чтобы не начать случайно качать сурс вместо 720п
        /// </summary>
        public bool takeOnlyPreferredQuality = true;

        /// <summary>
        /// 10 sec default
        /// </summary>
        public TimeSpan masterPlaylistRetryDelay = TimeSpan.FromSeconds(10);
        /// <summary>
        /// 1 sec default
        /// </summary>
        public TimeSpan minMediaPlaylistUpdateDelay = TimeSpan.FromSeconds(1);
        /// <summary>
        /// 3 sec default
        /// </summary>
        public TimeSpan maxMediaPlaylistUpdateDelay = TimeSpan.FromSeconds(3);

        /// <summary>
        /// 1 min default
        /// </summary>
        public TimeSpan accessTokenRetryDelay = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Если мастер лист выдал 403, нужно обновлять токен
        /// </summary>
        public bool automaticallyUpdateAccessToken = true;

        /// <summary>
        /// Если поставить тру, всё наёбнётся, наверное.
        /// TODO (именно здесь, ага) если времени нет, не делать предположение вокруг времени, только по намберу. 
        /// Должно сработать, если синхронизировать по времени намберы плейлистов
        /// </summary>
        public bool fastBread = false;
        public string userAgent = "Mozilla/5.0 (Linux; Android 6.0; Nexus 5 Build/MRA58N) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/86.0.4240.101 Mobile Safari/537.36";
        public IWebProxy? proxy = null;
    }
}