using ExtM3UPlaylistParser.Models;

namespace TwitchStreamDownloader.Download;

/// <summary>
/// Хранит текущий выбор даунлоадера и позволяет его менять извне.
/// <see cref="SelectionOverride"/>
/// </summary>
public class StreamSelection(VariantStream? selected, IReadOnlyList<VariantStream> options, Quality? lastUsedQuality)
{
    /// <summary>
    /// Какой вариант будет выбран, если не будет объекта в <see cref="SelectionOverride"/>
    /// </summary>
    public VariantStream? Selected { get; } = selected;

    /// <summary>
    /// Какие есть варианты на выборг
    /// </summary>
    public IReadOnlyList<VariantStream> Options { get; } = options;

    /// <summary>
    /// Какое качество было выбрано при прошлой загрузке.
    /// </summary>
    public Quality? LastUsedQuality { get; } = lastUsedQuality;

    /// <summary>
    /// Замени это на один из <see cref="Options"/>, если нужно.
    /// </summary>
    public VariantStream? SelectionOverride { get; set; }

    /// <summary>
    /// Если тру, вместо загрузки какого либо стрима из списка, попробует загрузить мастер плейлист ещё раз, если в <see cref="SelectionOverride"/> не будет выбора.
    /// По умолчанию тру, если в настройках брать только указанное качество, и его не было.
    /// </summary>
    public bool ResetPlaylist { get; set; } = false;
}