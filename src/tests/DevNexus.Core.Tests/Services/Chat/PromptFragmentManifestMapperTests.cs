using DevNexus.Core.Services.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// Prompt 片段清单 DTO 映射器测试。
/// </summary>
public sealed class PromptFragmentManifestMapperTests
{
    /// <summary>
    /// 映射为共享 DTO 时不应丢失字段。
    /// </summary>
    [Fact]
    public void ToDto_ShouldPreserveManifestFields()
    {
        var manifest = new[]
        {
            new PromptFragmentManifestItem
            {
                Slot = "SystemIdentity",
                Sequence = 10,
                Source = PromptFragmentSources.SystemIdentity,
                CharacterCount = 42,
                TextHash = "hash"
            }
        };

        var dto = PromptFragmentManifestMapper.ToDto(manifest);

        dto.Should().ContainSingle();
        dto[0].Slot.Should().Be("SystemIdentity");
        dto[0].Sequence.Should().Be(10);
        dto[0].Source.Should().Be(PromptFragmentSources.SystemIdentity);
        dto[0].CharacterCount.Should().Be(42);
        dto[0].TextHash.Should().Be("hash");
    }
}
