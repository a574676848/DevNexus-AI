using DevNexus.Core.Services.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// Prompt 缓存漂移分析器测试。
/// </summary>
public sealed class PromptCacheDriftAnalyzerTests
{
    /// <summary>
    /// 相同缓存键不应判定为漂移。
    /// </summary>
    [Fact]
    public void Analyze_ShouldReturnNone_WhenCacheKeyIsSame()
    {
        var reason = PromptCacheDriftAnalyzer.Analyze(
            CreateSnapshot("key", "stable", "tools"),
            CreateSnapshot("KEY", "stable", "tools"));

        reason.Should().Be(PromptCacheDriftReason.None);
    }

    /// <summary>
    /// 稳定前缀变化应优先归因到稳定前缀。
    /// </summary>
    [Fact]
    public void Analyze_ShouldReturnStablePrefixChanged_WhenStablePrefixChanged()
    {
        var reason = PromptCacheDriftAnalyzer.Analyze(
            CreateSnapshot("key-a", "stable-a", "tools"),
            CreateSnapshot("key-b", "stable-b", "tools"));

        reason.Should().Be(PromptCacheDriftReason.StablePrefixChanged);
    }

    /// <summary>
    /// 带片段清单时，稳定前缀变化应细分为片段清单变化。
    /// </summary>
    [Fact]
    public void Analyze_ShouldReturnStablePrefixManifestChanged_WhenManifestChanged()
    {
        var reason = PromptCacheDriftAnalyzer.Analyze(
            CreateSnapshot(
                "key-a",
                "stable-a",
                "tools",
                [CreateManifestItem("SystemIdentity", "hash-a")]),
            CreateSnapshot(
                "key-b",
                "stable-b",
                "tools",
                [CreateManifestItem("SystemIdentity", "hash-b")]));

        reason.Should().Be(PromptCacheDriftReason.StablePrefixManifestChanged);
    }

    /// <summary>
    /// 详细分析应返回发生变化的片段。
    /// </summary>
    [Fact]
    public void AnalyzeDetailed_ShouldReturnChangedFragments_WhenManifestChanged()
    {
        var analysis = PromptCacheDriftAnalyzer.AnalyzeDetailed(
            CreateSnapshot(
                "key-a",
                "stable-a",
                "tools",
                [
                    CreateManifestItem("SystemIdentity", "hash-a", sequence: 0, characterCount: 10),
                    CreateManifestItem("ToolGuidance", "hash-removed", sequence: 1, characterCount: 20)
                ]),
            CreateSnapshot(
                "key-b",
                "stable-b",
                "tools",
                [
                    CreateManifestItem("SystemIdentity", "hash-b", sequence: 0, characterCount: 11),
                    CreateManifestItem("OutputContract", "hash-added", sequence: 2, characterCount: 30)
                ]));

        analysis.Reason.Should().Be(PromptCacheDriftReason.StablePrefixManifestChanged);
        analysis.ChangedFragments.Should().HaveCount(3);
        analysis.ChangedFragments.Should().ContainEquivalentOf(new PromptFragmentDriftItem
        {
            Slot = "SystemIdentity",
            Sequence = 0,
            Kind = PromptFragmentDriftKind.Modified,
            PreviousCharacterCount = 10,
            CurrentCharacterCount = 11,
            PreviousTextHash = "hash-a",
            CurrentTextHash = "hash-b",
            PreviousSource = PromptFragmentSources.SystemIdentity,
            CurrentSource = PromptFragmentSources.SystemIdentity
        });
        analysis.ChangedFragments.Should().Contain(item =>
            item.Slot == "ToolGuidance" && item.Kind == PromptFragmentDriftKind.Removed);
        analysis.ChangedFragments.Should().Contain(item =>
            item.Slot == "OutputContract" && item.Kind == PromptFragmentDriftKind.Added);
    }

    /// <summary>
    /// 工具 Schema 变化应归因到工具 Schema。
    /// </summary>
    [Fact]
    public void Analyze_ShouldReturnToolSchemaChanged_WhenToolSchemaChanged()
    {
        var reason = PromptCacheDriftAnalyzer.Analyze(
            CreateSnapshot("key-a", "stable", "tools-a"),
            CreateSnapshot("key-b", "stable", "tools-b"));

        reason.Should().Be(PromptCacheDriftReason.ToolSchemaChanged);
    }

    /// <summary>
    /// 缓存键缺失时应返回缺失原因。
    /// </summary>
    [Fact]
    public void Analyze_ShouldReturnMissingCacheKey_WhenCacheKeyIsMissing()
    {
        var reason = PromptCacheDriftAnalyzer.Analyze(
            CreateSnapshot(null, "stable", "tools"),
            CreateSnapshot("key", "stable", "tools"));

        reason.Should().Be(PromptCacheDriftReason.MissingCacheKey);
    }

    private static PromptCacheSnapshot CreateSnapshot(
        string? promptCacheKey,
        string? stablePrefixHash,
        string? toolSchemaHash,
        IReadOnlyList<PromptFragmentManifestItem>? manifest = null)
    {
        return new PromptCacheSnapshot(promptCacheKey, stablePrefixHash, toolSchemaHash, manifest);
    }

    private static PromptFragmentManifestItem CreateManifestItem(
        string slot,
        string textHash,
        int sequence = 0,
        int characterCount = 10)
    {
        return new PromptFragmentManifestItem
        {
            Slot = slot,
            Sequence = sequence,
            Source = ResolveSource(slot),
            CharacterCount = characterCount,
            TextHash = textHash
        };
    }

    private static string ResolveSource(string slot)
    {
        return slot switch
        {
            nameof(PromptSlot.SystemIdentity) => PromptFragmentSources.SystemIdentity,
            nameof(PromptSlot.OutputContract) => PromptFragmentSources.OutputContract,
            nameof(PromptSlot.ToolGuidance) => PromptFragmentSources.ToolGuidance,
            _ => PromptFragmentSources.Unknown
        };
    }
}
