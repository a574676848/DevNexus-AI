using DevNexus.Core.Services.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 系统经验提纯 Prompt 构建器测试。
/// </summary>
public sealed class ExperienceDistillationPromptBuilderTests
{
    /// <summary>
    /// Prompt 应包含问题、回答和稳定输出协议。
    /// </summary>
    [Fact]
    public void Build_ShouldIncludeQuestionAnswerAndOutputProtocol()
    {
        var prompt = ExperienceDistillationPromptBuilder.Build("如何修复构建失败？", "先运行 dotnet build。");

        prompt.Content.Should().Contain("【用户问题】");
        prompt.Content.Should().Contain("如何修复构建失败？");
        prompt.Content.Should().Contain("【助手回答】");
        prompt.Content.Should().Contain("先运行 dotnet build。");
        prompt.Content.Should().Contain(ExperienceDistillationOutputProtocol.Version);
        prompt.Content.Should().Contain(ExperienceDistillationOutputProtocol.IntentMarker);
        prompt.Content.Should().Contain(ExperienceDistillationOutputProtocol.NoValueMarker);
    }

    /// <summary>
    /// Prompt 应包含高价值信号白名单和拒绝条件。
    /// </summary>
    [Fact]
    public void Build_ShouldIncludeValueWhitelistAndSkipConditions()
    {
        var prompt = ExperienceDistillationPromptBuilder.Build("后续如何处理？", "必须记录架构决策。");

        foreach (var signal in ExperienceDistillationOutputProtocol.HighValueSignals)
        {
            prompt.Content.Should().Contain(signal);
        }

        foreach (var condition in ExperienceDistillationOutputProtocol.SkipConditions)
        {
            prompt.Content.Should().Contain(condition);
        }
    }

    /// <summary>
    /// Prompt 应声明长期经验正文的长度和原始记录禁入边界。
    /// </summary>
    [Fact]
    public void Build_ShouldIncludePersistentMemoryQualityBoundaries()
    {
        var prompt = ExperienceDistillationPromptBuilder.Build("后续如何处理？", "必须记录架构决策。");

        prompt.Content.Should().Contain(ExperienceDistillationOutputProtocol.MaximumSopCharacters.ToString());
        prompt.Content.Should().Contain("禁止把原始 QA、聊天日志、工具输出、命令输出或临时调试记录写入 SOP。");
    }

    /// <summary>
    /// Prompt 指纹应稳定，便于后台提纯链路低噪追踪协议变化。
    /// </summary>
    [Fact]
    public void Build_ShouldCreateStableFingerprint()
    {
        var left = ExperienceDistillationPromptBuilder.Build("后续如何处理？", "必须记录架构决策。");
        var right = ExperienceDistillationPromptBuilder.Build("后续如何处理？", "必须记录架构决策。");

        left.Fingerprint.Should().NotBeEmpty();
        left.Fingerprint.Should().Be(right.Fingerprint);
    }

    /// <summary>
    /// Prompt 内容变化时指纹应变化。
    /// </summary>
    [Fact]
    public void Build_ShouldChangeFingerprint_WhenPromptContentChanges()
    {
        var left = ExperienceDistillationPromptBuilder.Build("后续如何处理？", "必须记录架构决策。");
        var right = ExperienceDistillationPromptBuilder.Build("后续如何处理？", "必须记录不同的约束。");

        left.Fingerprint.Should().NotBe(right.Fingerprint);
    }
}
