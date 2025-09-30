using System.Text.Json.Serialization;

namespace TwitchStreamDownloader.Models;

/// <summary>
/// походу, все поля есть всегда, но мало ли придумают че.
/// Но строка пустая, если ничего нет. не нулл.
/// </summary>
public class AccessTokenValue
{
    public class AuthorizationData(bool? forbidden, string? reason)
    {
        [JsonPropertyName("forbidden")] public bool? Forbidden { get; } = forbidden;
        [JsonPropertyName("reason")] public string? Reason { get; } = reason;
    }

    public class PrivateData(bool? allowedToView)
    {
        [JsonPropertyName("allowed_to_view")] public bool? AllowedToView { get; } = allowedToView;
    }

    public AccessTokenValue(bool? adblock, AuthorizationData? authorization, bool? blackoutEnabled, string? channel,
        string? channelId, string? geoblockReason, string? deviceId, long? expires, bool? extendedHistoryAllowed,
        bool? hideAds, bool? httpsRequired, bool? mature, bool? partner, string? platform,
        string? playerType, PrivateData? @private, bool? privileged, string? role, bool? serverAds, bool? showAds,
        bool? subscriber, bool? turbo, ulong? userId, string? userIp, int? version)
    {
        Adblock = adblock;
        Authorization = authorization;
        BlackoutEnabled = blackoutEnabled;
        Channel = channel;
        ChannelId = channelId;
        GeoblockReason = geoblockReason;
        DeviceId = deviceId;
        Expires = expires;
        ExtendedHistoryAllowed = extendedHistoryAllowed;
        HideAds = hideAds;
        HttpsRequired = httpsRequired;
        Mature = mature;
        Partner = partner;
        Platform = platform;
        PlayerType = playerType;
        Private = @private;
        Privileged = privileged;
        Role = role;
        ServerAds = serverAds;
        ShowAds = showAds;
        Subscriber = subscriber;
        Turbo = turbo;
        UserId = userId;
        UserIp = userIp;
        Version = version;
    }

    [JsonPropertyName("adblock")] public bool? Adblock { get; }

    /// <summary>
    /// формиден фолс было бы неплохо
    /// </summary>
    [JsonPropertyName("authorization")]
    public AuthorizationData? Authorization { get; }

    [JsonPropertyName("blackout_enabled")] public bool? BlackoutEnabled { get; }

    [JsonPropertyName("channel")] public string? Channel { get; }

    [JsonPropertyName("channel_id")] public string? ChannelId { get; }

    //впадлу
    //chansub

    [JsonPropertyName("geoblock_reason")] public string? GeoblockReason { get; }

    [JsonPropertyName("device_id")] public string? DeviceId { get; }

    /// <summary>
    /// DateTimeOffset.FromUnixTimeSeconds
    /// </summary>
    [JsonPropertyName("expires")]
    public long? Expires { get; }

    [JsonPropertyName("extended_history_allowed")]
    public bool? ExtendedHistoryAllowed { get; }

    //почему то пустая была
    //впадлу
    //[JsonPropertyName("game")]
    // public string? Game { get; }

    [JsonPropertyName("hide_ads")] public bool? HideAds { get; }

    [JsonPropertyName("https_required")] public bool? HttpsRequired { get; }

    [JsonPropertyName("mature")] public bool? Mature { get; }

    [JsonPropertyName("partner")] public bool? Partner { get; }

    /// <summary>
    /// В браузере было "web"
    /// </summary>
    [JsonPropertyName("platform")]
    public string? Platform { get; }

    /// <summary>
    /// В браузере было "site"
    /// </summary>
    [JsonPropertyName("player_type")]
    public string? PlayerType { get; }

    [JsonPropertyName("private")] public PrivateData? Private { get; }

    [JsonPropertyName("privileged")] public bool? Privileged { get; }

    /// <summary>
    /// Пустая
    /// </summary>
    [JsonPropertyName("role")]
    public string? Role { get; }

    [JsonPropertyName("server_ads")] public bool? ServerAds { get; }

    [JsonPropertyName("show_ads")] public bool? ShowAds { get; }

    [JsonPropertyName("subscriber")] public bool? Subscriber { get; }

    [JsonPropertyName("turbo")] public bool? Turbo { get; }

    [JsonPropertyName("user_id")] public ulong? UserId { get; }

    [JsonPropertyName("user_ip")] public string? UserIp { get; }

    [JsonPropertyName("version")] public int? Version { get; }
}