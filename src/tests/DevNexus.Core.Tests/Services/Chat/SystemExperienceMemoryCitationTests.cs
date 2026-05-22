using DevNexus.Core.Services.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 系统经验记忆引用事实测试。
/// </summary>
public sealed class SystemExperienceMemoryCitationTests
{
    /// <summary>
    /// 引用事实应从上下文标签中读取来源会话、价值信号和协议版本。
    /// </summary>
    [Fact]
    public void FromContextTags_ShouldCreateStructuredCitation()
    {
        var experienceId = Guid.NewGuid();
        var sourceSessionId = Guid.NewGuid();
        const string PromptFingerprint = "prompt-fingerprint";
        var citation = SystemExperienceMemoryCitation.FromContextTags(
            experienceId,
            string.Join(
                ",",
                ExperienceDistillationOutputProtocol.ContextTag,
                ExperienceDistillationOutputProtocol.ValueSignalTagPrefix + "流程",
                ExperienceDistillationOutputProtocol.DistillationPromptFingerprintTagPrefix + PromptFingerprint,
                ExperienceDistillationOutputProtocol.SourceSessionTagPrefix + sourceSessionId.ToString("D")));

        citation.ExperienceId.Should().Be(experienceId);
        citation.SourceSessionId.Should().Be(sourceSessionId);
        citation.ValueSignalKeyword.Should().Be("流程");
        citation.DistillationProtocol.Should().Be(ExperienceDistillationOutputProtocol.Version);
        citation.DistillationPromptFingerprint.Should().Be(PromptFingerprint);
        citation.CitationFingerprint.Should().NotBeEmpty();
    }

    /// <summary>
    /// 相同引用事实应生成稳定指纹。
    /// </summary>
    [Fact]
    public void CitationFingerprint_ShouldBeStable_ForSameFacts()
    {
        var experienceId = Guid.NewGuid();
        var sourceSessionId = Guid.NewGuid();
        var left = new SystemExperienceMemoryCitation
        {
            ExperienceId = experienceId,
            SourceSessionId = sourceSessionId,
            ValueSignalKeyword = "流程",
            DistillationProtocol = ExperienceDistillationOutputProtocol.Version,
            DistillationPromptFingerprint = "prompt-fingerprint"
        };
        var right = new SystemExperienceMemoryCitation
        {
            ExperienceId = experienceId,
            SourceSessionId = sourceSessionId,
            ValueSignalKeyword = "流程",
            DistillationProtocol = ExperienceDistillationOutputProtocol.Version,
            DistillationPromptFingerprint = "prompt-fingerprint"
        };

        left.CitationFingerprint.Should().Be(right.CitationFingerprint);
    }

    /// <summary>
    /// 提纯 Prompt 指纹变化应改变引用指纹，避免不同协议边界被误判为同一引用。
    /// </summary>
    [Fact]
    public void CitationFingerprint_ShouldChange_WhenPromptFingerprintChanges()
    {
        var experienceId = Guid.NewGuid();
        var sourceSessionId = Guid.NewGuid();
        var left = new SystemExperienceMemoryCitation
        {
            ExperienceId = experienceId,
            SourceSessionId = sourceSessionId,
            ValueSignalKeyword = "流程",
            DistillationProtocol = ExperienceDistillationOutputProtocol.Version,
            DistillationPromptFingerprint = "prompt-a"
        };
        var right = new SystemExperienceMemoryCitation
        {
            ExperienceId = experienceId,
            SourceSessionId = sourceSessionId,
            ValueSignalKeyword = "流程",
            DistillationProtocol = ExperienceDistillationOutputProtocol.Version,
            DistillationPromptFingerprint = "prompt-b"
        };

        left.CitationFingerprint.Should().NotBe(right.CitationFingerprint);
    }

    /// <summary>
    /// 未落盘经验缺少经验 ID 时仍应生成可追踪指纹。
    /// </summary>
    [Fact]
    public void CitationFingerprint_ShouldExist_WhenExperienceIdIsMissing()
    {
        var citation = new SystemExperienceMemoryCitation
        {
            SourceSessionId = Guid.NewGuid(),
            ValueSignalKeyword = "流程",
            DistillationProtocol = ExperienceDistillationOutputProtocol.Version
        };

        citation.ExperienceId.Should().BeNull();
        citation.CitationFingerprint.Should().NotBeEmpty();
    }

    /// <summary>
    /// 未落盘提纯引用应保留来源会话、价值信号、协议版本和 Prompt 指纹。
    /// </summary>
    [Fact]
    public void CreateUnpersistedDistillationCitation_ShouldKeepPromptFingerprint()
    {
        var sourceSessionId = Guid.NewGuid();
        var citation = SystemExperienceMemoryCitation.CreateUnpersistedDistillationCitation(
            sourceSessionId,
            "踩坑",
            "prompt-fingerprint");

        citation.ExperienceId.Should().BeNull();
        citation.SourceSessionId.Should().Be(sourceSessionId);
        citation.ValueSignalKeyword.Should().Be("踩坑");
        citation.DistillationProtocol.Should().Be(ExperienceDistillationOutputProtocol.Version);
        citation.DistillationPromptFingerprint.Should().Be("prompt-fingerprint");
        citation.CitationFingerprint.Should().NotBeEmpty();
    }

    /// <summary>
    /// 未落盘提纯引用的 Prompt 指纹变化应改变引用指纹。
    /// </summary>
    [Fact]
    public void CreateUnpersistedDistillationCitation_ShouldChangeFingerprint_WhenPromptFingerprintChanges()
    {
        var sourceSessionId = Guid.NewGuid();
        var left = SystemExperienceMemoryCitation.CreateUnpersistedDistillationCitation(
            sourceSessionId,
            "踩坑",
            "prompt-a");
        var right = SystemExperienceMemoryCitation.CreateUnpersistedDistillationCitation(
            sourceSessionId,
            "踩坑",
            "prompt-b");

        left.CitationFingerprint.Should().NotBe(right.CitationFingerprint);
    }

    /// <summary>
    /// 引用片段缺少来源字段时应输出 none，避免模型猜测来源。
    /// </summary>
    [Fact]
    public void ToPromptBlock_ShouldUseNone_WhenFactsAreMissing()
    {
        var block = SystemExperienceMemoryCitation.Empty.ToPromptBlock();

        block.Should().Contain("ExperienceId: none");
        block.Should().Contain("SourceSessionId: none");
        block.Should().Contain("ValueSignal: none");
        block.Should().Contain("DistillationProtocol: none");
        block.Should().Contain("DistillationPromptFingerprint: none");
        block.Should().Contain("CitationFingerprint:");
    }
}
