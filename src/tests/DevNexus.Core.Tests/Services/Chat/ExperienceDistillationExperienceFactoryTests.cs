using DevNexus.Core.Services.Chat;
using DevNexus.Domain.Models;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 系统经验实体工厂测试。
/// </summary>
public sealed class ExperienceDistillationExperienceFactoryTests
{
    /// <summary>
    /// 工厂应统一设置系统经验默认生命周期指标。
    /// </summary>
    [Fact]
    public void CreateQaExperience_ShouldSetDefaultLifecycleMetrics()
    {
        var matchedAt = DateTime.UtcNow;
        var experience = ExperienceDistillationExperienceFactory.CreateQaExperience(
            new ExperienceDistillationParseResult
            {
                HasValue = true,
                Intent = "修复构建失败",
                SolutionSop = "运行构建并分析错误。"
            },
            matchedAt);

        experience.Type.Should().Be(ExperienceType.QA);
        experience.Intent.Should().Be("修复构建失败");
        experience.SolutionSop.Should().Be("运行构建并分析错误。");
        experience.ContextTags.Should().Contain(ExperienceDistillationOutputProtocol.ContextTag);
        experience.UtilityScore.Should().Be(ExperienceDistillationExperienceFactory.InitialUtilityScore);
        experience.UsageCount.Should().Be(ExperienceDistillationExperienceFactory.InitialUsageCount);
        experience.LastMatchedAt.Should().Be(matchedAt);
    }

    /// <summary>
    /// 工厂应把自我迭代调度上下文写入经验标签，便于后续回放和复盘追踪。
    /// </summary>
    [Fact]
    public void CreateQaExperience_ShouldIncludeScheduleContextTags()
    {
        var sourceSessionId = Guid.NewGuid();
        var promptFingerprint = PromptFingerprint.ComputeHash("prompt");
        var experience = ExperienceDistillationExperienceFactory.CreateQaExperience(
            new ExperienceDistillationParseResult
            {
                HasValue = true,
                Intent = "沉淀上下文治理经验",
                SolutionSop = "记录候选原因和压缩指纹。"
            },
            DateTime.UtcNow,
            ExperienceDistillationScheduleContext.Create(
                "summary-compression-resolved",
                "summary-compression",
                "fingerprint"),
            "流程",
            sourceSessionId,
            promptFingerprint);

        experience.ContextTags.Should().Contain(
            ExperienceDistillationOutputProtocol.CandidateReasonTagPrefix + "summary-compression-resolved");
        experience.ContextTags.Should().Contain(
            ExperienceDistillationOutputProtocol.ContextPressureReasonTagPrefix + "summary-compression");
        experience.ContextTags.Should().Contain(
            ExperienceDistillationOutputProtocol.ContextCompressionFingerprintTagPrefix + "fingerprint");
        experience.ContextTags.Should().Contain(
            ExperienceDistillationOutputProtocol.DistillationPromptFingerprintTagPrefix + promptFingerprint);
        experience.ContextTags.Should().Contain(
            ExperienceDistillationOutputProtocol.ValueSignalTagPrefix + "流程");
        experience.ContextTags.Should().Contain(
            ExperienceDistillationOutputProtocol.SourceSessionTagPrefix + sourceSessionId.ToString("D"));
    }
}
