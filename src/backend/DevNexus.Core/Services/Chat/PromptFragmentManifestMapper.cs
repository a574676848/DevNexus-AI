using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Prompt 片段清单 DTO 映射器。
/// </summary>
internal static class PromptFragmentManifestMapper
{
    /// <summary>
    /// 映射为共享 DTO。
    /// </summary>
    public static IReadOnlyList<PromptFragmentManifestItemDto> ToDto(
        IReadOnlyList<PromptFragmentManifestItem> manifest)
    {
        return manifest
            .Select(item => new PromptFragmentManifestItemDto
            {
                Slot = item.Slot,
                Sequence = item.Sequence,
                Source = item.Source,
                CharacterCount = item.CharacterCount,
                TextHash = item.TextHash
            })
            .ToList();
    }
}
