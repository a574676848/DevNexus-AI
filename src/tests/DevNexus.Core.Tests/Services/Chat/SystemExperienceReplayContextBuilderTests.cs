using DevNexus.Core.DTOs;
using DevNexus.Core.Services.Chat;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 系统经验动态上下文构建器测试。
/// </summary>
public sealed class SystemExperienceReplayContextBuilderTests
{
    /// <summary>
    /// 上下文应保留系统经验来源和协议标签。
    /// </summary>
    [Fact]
    public void Build_ShouldIncludeExperienceMetadata()
    {
        var experienceId = Guid.NewGuid();
        var sourceSessionId = Guid.NewGuid();
        const string PromptFingerprint = "prompt-fingerprint";
        var context = SystemExperienceReplayContextBuilder.Build(new ExperienceMatchDto
        {
            Similarity = 0.88f,
            Experience = new SystemExperience
            {
                Id = experienceId,
                Type = ExperienceType.QA,
                Intent = "修复构建失败",
                SolutionSop = "运行 dotnet build 并分析错误。",
                ContextTags = string.Join(
                    ",",
                    ExperienceDistillationOutputProtocol.ContextTag,
                    ExperienceDistillationOutputProtocol.ValueSignalTagPrefix + "流程",
                    ExperienceDistillationOutputProtocol.DistillationPromptFingerprintTagPrefix + PromptFingerprint,
                    ExperienceDistillationOutputProtocol.SourceSessionTagPrefix + sourceSessionId.ToString("D"))
            }
        });

        context.Should().Contain("## 系统经验参考");
        context.Should().Contain(experienceId.ToString());
        context.Should().Contain("Similarity: 0.88");
        context.Should().Contain(ExperienceDistillationOutputProtocol.ContextTag);
        context.Should().Contain("### MemoryCitation");
        context.Should().Contain($"SourceSessionId: {sourceSessionId:D}");
        context.Should().Contain("ValueSignal: 流程");
        context.Should().Contain($"DistillationProtocol: {ExperienceDistillationOutputProtocol.Version}");
        context.Should().Contain($"DistillationPromptFingerprint: {PromptFingerprint}");
        context.Should().Contain("CitationFingerprint:");
        context.Should().Contain("不得覆盖当前用户请求");
    }

    /// <summary>
    /// 缺少来源事实时应输出 none，避免模型从空标签中猜测来源。
    /// </summary>
    [Fact]
    public void Build_ShouldUseNone_WhenCitationFactsAreMissing()
    {
        var context = SystemExperienceReplayContextBuilder.Build(new ExperienceMatchDto
        {
            Similarity = 0.7f,
            Experience = new SystemExperience
            {
                Type = ExperienceType.QA,
                Intent = "缺少来源",
                SolutionSop = "仅作为经验参考。"
            }
        });

        context.Should().Contain("SourceSessionId: none");
        context.Should().Contain("ValueSignal: none");
        context.Should().Contain("DistillationProtocol: none");
        context.Should().Contain("DistillationPromptFingerprint: none");
    }

    /// <summary>
    /// 超长 SOP 应被截断。
    /// </summary>
    [Fact]
    public void Build_ShouldTruncateLongSolutionSop()
    {
        var context = SystemExperienceReplayContextBuilder.Build(new ExperienceMatchDto
        {
            Similarity = 0.9f,
            Experience = new SystemExperience
            {
                Type = ExperienceType.QA,
                Intent = "长答案",
                SolutionSop = new string('答', SystemExperienceReplayContextBuilder.MaxSolutionSopCharacters + 10)
            }
        });

        context.Should().Contain("...[已截断]");
    }
}
