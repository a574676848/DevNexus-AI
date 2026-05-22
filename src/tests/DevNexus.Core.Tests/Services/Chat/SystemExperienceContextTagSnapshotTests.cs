using DevNexus.Core.Services.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 系统经验上下文标签快照测试。
/// </summary>
public sealed class SystemExperienceContextTagSnapshotTests
{
    /// <summary>
    /// 标签快照应解析提纯协议、自我迭代事实和语义指纹。
    /// </summary>
    [Fact]
    public void Parse_ShouldExtractStructuredMemoryFacts()
    {
        var sourceSessionId = Guid.NewGuid();
        var promptFingerprint = PromptFingerprint.ComputeHash("distillation-prompt");
        var snapshot = SystemExperienceContextTagSnapshot.Parse(string.Join(
            ",",
            ExperienceDistillationOutputProtocol.ContextTag,
            ExperienceDistillationOutputProtocol.CandidateReasonTagPrefix + "summary-compression-resolved",
            ExperienceDistillationOutputProtocol.ContextPressureReasonTagPrefix + "summary-compression",
            ExperienceDistillationOutputProtocol.ContextCompressionFingerprintTagPrefix + "fingerprint",
            ExperienceDistillationOutputProtocol.DistillationPromptFingerprintTagPrefix + promptFingerprint,
            ExperienceDistillationOutputProtocol.ValueSignalTagPrefix + "流程",
            ExperienceDistillationOutputProtocol.SourceSessionTagPrefix + sourceSessionId.ToString("D"),
            SystemExperienceFingerprint.ContextTagPrefix + "semantic"));

        snapshot.HasDistillationProtocol.Should().BeTrue();
        snapshot.DistillationProtocol.Should().Be(ExperienceDistillationOutputProtocol.Version);
        snapshot.HasSelfIterationFacts.Should().BeTrue();
        snapshot.CandidateReason.Should().Be("summary-compression-resolved");
        snapshot.ContextPressureReason.Should().Be("summary-compression");
        snapshot.ContextCompressionSummaryFingerprint.Should().Be("fingerprint");
        snapshot.DistillationPromptFingerprint.Should().Be(promptFingerprint);
        snapshot.ValueSignalKeyword.Should().Be("流程");
        snapshot.SourceSessionId.Should().Be(sourceSessionId);
        snapshot.SemanticFingerprint.Should().Be("semantic");
    }

    /// <summary>
    /// 空标签应生成空快照，避免编排日志误报记忆事实。
    /// </summary>
    [Fact]
    public void Parse_ShouldReturnEmpty_WhenTagsAreBlank()
    {
        var snapshot = SystemExperienceContextTagSnapshot.Parse("  ");

        snapshot.HasDistillationProtocol.Should().BeFalse();
        snapshot.HasSelfIterationFacts.Should().BeFalse();
        snapshot.DistillationPromptFingerprint.Should().BeEmpty();
        snapshot.ValueSignalKeyword.Should().BeEmpty();
        snapshot.SourceSessionId.Should().BeNull();
        snapshot.SemanticFingerprint.Should().BeEmpty();
    }
}
