namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Prompt 片段清单项。
/// </summary>
public sealed record PromptFragmentManifestItem
{
    /// <summary>
    /// 片段槽位。
    /// </summary>
    public string Slot { get; init; } = string.Empty;

    /// <summary>
    /// 片段顺序。
    /// </summary>
    public int Sequence { get; init; }

    /// <summary>
    /// 片段来源标识。
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// 片段字符数。
    /// </summary>
    public int CharacterCount { get; init; }

    /// <summary>
    /// 片段内容指纹。
    /// </summary>
    public string TextHash { get; init; } = string.Empty;
}

/// <summary>
/// Prompt 片段清单构建器。
/// </summary>
internal static class PromptFragmentManifestBuilder
{
    /// <summary>
    /// 构建稳定排序后的 Prompt 片段清单。
    /// </summary>
    public static IReadOnlyList<PromptFragmentManifestItem> Build(IEnumerable<PromptFragment> fragments)
    {
        return fragments
            .Where(fragment => !string.IsNullOrWhiteSpace(fragment.Text))
            .OrderBy(fragment => fragment.Slot)
            .ThenBy(fragment => fragment.Sequence)
            .Select(fragment =>
            {
                var text = fragment.Text.Trim();
                return new PromptFragmentManifestItem
                {
                    Slot = fragment.Slot.ToString(),
                    Sequence = fragment.Sequence,
                    Source = fragment.Source,
                    CharacterCount = text.Length,
                    TextHash = PromptFingerprint.ComputeHash(text)
                };
            })
            .ToList();
    }
}
