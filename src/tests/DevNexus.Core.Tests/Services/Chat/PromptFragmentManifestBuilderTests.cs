using DevNexus.Core.Services.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// Prompt 片段清单构建器测试。
/// </summary>
public sealed class PromptFragmentManifestBuilderTests
{
    /// <summary>
    /// 片段清单应按组合器相同顺序输出。
    /// </summary>
    [Fact]
    public void Build_ShouldUseComposerOrdering()
    {
        var manifest = PromptFragmentManifestBuilder.Build(
        [
            PromptFragment.AgentWorkflow("workflow"),
            PromptFragment.SystemIdentity("identity"),
            PromptFragment.ToolGuidance("tool", sequence: 20),
            PromptFragment.ToolGuidance("tool-first", sequence: 10)
        ]);

        manifest.Select(item => item.Slot).Should().Equal(
            nameof(PromptSlot.SystemIdentity),
            nameof(PromptSlot.ToolGuidance),
            nameof(PromptSlot.ToolGuidance),
            nameof(PromptSlot.AgentWorkflow));
        manifest.Select(item => item.Sequence).Should().Equal(0, 10, 20, 0);
        manifest.Select(item => item.Source).Should().Equal(
            PromptFragmentSources.SystemIdentity,
            PromptFragmentSources.ToolGuidance,
            PromptFragmentSources.ToolGuidance,
            PromptFragmentSources.AgentWorkflow);
    }

    /// <summary>
    /// 清单应过滤空白片段并记录 trim 后字符数。
    /// </summary>
    [Fact]
    public void Build_ShouldFilterBlankAndTrimCharacterCount()
    {
        var manifest = PromptFragmentManifestBuilder.Build(
        [
            PromptFragment.SystemIdentity(" identity "),
            PromptFragment.OutputContract("   ")
        ]);

        manifest.Should().ContainSingle();
        manifest[0].CharacterCount.Should().Be("identity".Length);
        manifest[0].Source.Should().Be(PromptFragmentSources.SystemIdentity);
    }

    /// <summary>
    /// 片段内容变化应反映到清单指纹。
    /// </summary>
    [Fact]
    public void Build_ShouldChangeTextHash_WhenTextChanges()
    {
        var first = PromptFragmentManifestBuilder.Build(
        [
            PromptFragment.SystemIdentity("identity-a")
        ]);
        var second = PromptFragmentManifestBuilder.Build(
        [
            PromptFragment.SystemIdentity("identity-b")
        ]);

        first[0].TextHash.Should().NotBe(second[0].TextHash);
    }

    /// <summary>
    /// 自定义片段来源为空时应归一为 unknown。
    /// </summary>
    [Fact]
    public void Build_ShouldNormalizeBlankSource()
    {
        var manifest = PromptFragmentManifestBuilder.Build(
        [
            PromptFragment.Create(PromptSlot.ToolGuidance, "tool", source: " ")
        ]);

        manifest.Should().ContainSingle();
        manifest[0].Source.Should().Be(PromptFragmentSources.Unknown);
    }

    /// <summary>
    /// 动态上下文清单应保留来源与内容摘要。
    /// </summary>
    [Fact]
    public void Build_ShouldSupportDynamicContextManifest()
    {
        var manifest = PromptFragmentManifestBuilder.Build(
        [
            PromptFragment.DynamicContext(
                "当前工具面板：终端",
                sequence: 10,
                PromptFragmentSources.DynamicToolSelection)
        ]);

        manifest.Should().ContainSingle();
        manifest[0].Slot.Should().Be(nameof(PromptSlot.DynamicContext));
        manifest[0].Sequence.Should().Be(10);
        manifest[0].Source.Should().Be(PromptFragmentSources.DynamicToolSelection);
        manifest[0].CharacterCount.Should().Be("当前工具面板：终端".Length);
        manifest[0].TextHash.Should().NotBeNullOrWhiteSpace();
    }
}
