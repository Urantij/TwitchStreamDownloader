namespace TwitchStreamDownloader.Models
{
    /// <summary>
    /// походу, все поля есть всегда, но мало ли придумают че.
    /// Но строка пустая, если ничего нет. не нулл.
    /// </summary>
    public class AccessTokenValue
    {
        public class Authorization
        {
            public bool? forbidden;
            public string? reason;
        }

        public class Private
        {
            public bool? allowed_to_view;
        }

        public bool? adblock;

        /// <summary>
        /// формиден фолс было бы неплохо
        /// </summary>
        public Authorization? authorization;

        public bool? blackout_enabled;

        public string? channel;

        public string? channel_id;

        //впадлу
        //chansub

        public string? geoblock_reason;

        public string? device_id;

        /// <summary>
        /// DateTimeOffset.FromUnixTimeSeconds
        /// Не нулл чтобы крашилось, если нет. а так только оно нужно
        /// </summary>
        public long expires;

        public bool? extended_history_allowed;

        //почему то пустая была
        //впадлу
        //public string? game;

        public bool? hide_ads;

        public bool? https_required;

        public bool? mature;

        public bool? partner;

        /// <summary>
        /// В браузере было "web"
        /// </summary>
        public string? platform;

        /// <summary>
        /// В браузере было "site"
        /// </summary>
        public string? player_type;

        public Private? @private;

        public bool? privileged;

        /// <summary>
        /// Пустая
        /// </summary>
        public string? role;

        public bool? server_ads;

        public bool? show_ads;

        public bool? subscriber;

        public bool? turbo;

        public ulong? user_id;

        public string? user_ip;

        public int? version;
    }
}