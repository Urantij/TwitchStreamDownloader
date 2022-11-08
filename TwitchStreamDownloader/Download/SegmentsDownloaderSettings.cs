using System.Net;
using ExtM3UPlaylistParser.Models;

namespace TwitchStreamDownloader.Download
{
    public class SegmentsDownloaderSettings
    {
        //Если есть качество, возьмёт это качество. По возможности также возьмёт фпс.
        //Если есть фпс, но нет качествао, то па е бать ыхых
        //TODO если нулл, должен брать сурс, но скорее всего будет брать срань
        public Resolution? preferredResolution = null;
        /// <summary>
        /// 60
        /// 30
        /// </summary>
        public float? preferredFps = null;

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
        /// Вечно у твича отваливается жопа. Первый ретрай будет ждать меньше.
        /// Алсо, если токен меняется автомат с oauth на нул, то тоже будет это время
        /// </summary>
        public TimeSpan shortAccessTokenRetryDelay = TimeSpan.FromSeconds(5);

        /// <summary>
        /// 20 sec default
        /// </summary>
        public TimeSpan accessTokenRetryDelay = TimeSpan.FromSeconds(20);

        /// <summary>
        /// Если мастер лист выдал 403, нужно обновлять токен
        /// </summary>
        public bool automaticallyUpdateAccessToken = true;

        /// <summary>
        /// Если качатор с oauth токеном, то можно после лимита вылетов подряд попытки скачать токен сбросить oauth в нул.
        /// По дефолту 3, -1 отключает.
        /// </summary>
        public int oauthTokenFailedAttemptsLimit = 3;

        /// <summary>
        /// Если поставить тру, всё наёбнётся, наверное.
        /// TODO (именно здесь, ага) если времени нет, не делать предположение вокруг времени, только по намберу. 
        /// Должно сработать, если синхронизировать по времени намберы плейлистов
        /// </summary>
        public bool fastBread = false;
    }
}