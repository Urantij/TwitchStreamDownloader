using TwitchStreamDownloader.Download;

namespace TwitchStreamDownloader.Resources;

public class StreamSegment
{
    public Uri Uri { get; }
    public string? Title { get; }
    public int MediaSequenceNumber { get; }

    /// <summary>
    /// Секунды
    /// </summary>
    public float Duration { get; }

    public DateTimeOffset ProgramDate { get; }

    public Quality Quality { get; }

    /// <summary>
    /// URI="https://c513a515af41.j.cloudfront.hls.ttvnw.net/v1/segment/CtQCr-fbhmD925_INBtMddsm3cL7Q7iWFLxIAcMsUgwd6KloQmeimoczDjyvfuHfeskmaIlvQqmjrxvcCjAjR7-PW5xm-fKyPrL9bq3ETpIElmYpmdjftkuEbXfrnE4o68xsB5Bw0_b_o4RI57zX6StQULNgtfstOgEhXXQDqbut98BFGe_v2Nb3pjxJ_5f1tWL9RnMsSwfomgqixSDIMBkEFWS5NoZ_m_mVd0zgWAYHRsKGYC1TZ3zh1-D5lz1WE8UQNawNHAlmHNDbHOvFYl3T52n0VyOaqREMxgc1IV1lOFqKxf5iezkCpNiXLwAclooUV50Bn1Fo9NKJajjXiQeqg7vfxmaY5BhY2_E9FIdL5B4tKjKjoVFf7Z0ZabQQ1dxWZN0po1TEzfH1ZVmvzidCFyX5tRMhfbvsZjfik4S_pJmyk2DKCJuU04bHnCwT-gXL4EBiXRoMTPojpbeZr7-0EemaIAEqCWV1LXdlc3QtMjDODA.mp4?dna=CmmxpHN7Z82FYvtUvmIv69P131JG5Mtu4NPKnUIyptQnpvWCHpCHmoCYGAnLQLFK9OioFzPt-ai70gS5mNEasHXYF_j5G40Ngwk2c192L58qYyxWpRzRFDO86pFpF7VqMSGsPQixlhpADk4aDGhh-OiYrfc0twVu2CABKglldS13ZXN0LTIwzgw"
    /// </summary>
    public string? MapValue { get; }

    public StreamSegment(Uri uri, string? title, int mediaSequenceNumber, float duration, DateTimeOffset programDate,
        Quality quality, string? mapValue)
    {
        this.Uri = uri;
        this.Title = title;
        this.MediaSequenceNumber = mediaSequenceNumber;
        this.Duration = duration;
        this.ProgramDate = programDate;
        this.Quality = quality;
        this.MapValue = mapValue;
    }

    public bool IsLive() => string.Equals(Title, "live", StringComparison.OrdinalIgnoreCase);
}