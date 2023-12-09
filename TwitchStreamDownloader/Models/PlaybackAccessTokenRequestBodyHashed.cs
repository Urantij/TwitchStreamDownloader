namespace TwitchStreamDownloader.Models;

class PlaybackAccessTokenRequestBodyHashed
{
    public class Extensions
    {
        public class PersistedQuery
        {
            public int version;
            public string sha256Hash;

            public PersistedQuery(int version, string sha256Hash)
            {
                this.version = version;
                this.sha256Hash = sha256Hash;
            }
        }

        public PersistedQuery persistedQuery;

        public Extensions(PersistedQuery persistedQuery)
        {
            this.persistedQuery = persistedQuery;
        }
    }

    public class Variables
    {
        public bool isLive;
        public string login;
        public bool isVod;
        public string vodID;
        public string playerType;

        public Variables(bool isLive, string login, bool isVod, string vodID, string playerType)
        {
            this.isLive = isLive;
            this.login = login;
            this.isVod = isVod;
            this.vodID = vodID;
            this.playerType = playerType;
        }
    }

    public string operationName = "PlaybackAccessToken";
    public Extensions extensions;
    public Variables variables;

    public PlaybackAccessTokenRequestBodyHashed(string hash, string channel)
    {
        extensions = new Extensions(new Extensions.PersistedQuery(1, hash));

        variables = new Variables(true, channel, false, "", "site");
    }
}
