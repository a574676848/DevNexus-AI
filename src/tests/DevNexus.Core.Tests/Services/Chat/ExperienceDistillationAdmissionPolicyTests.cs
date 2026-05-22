using DevNexus.Core.Services.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 系统经验提纯准入策略测试。
/// </summary>
public sealed class ExperienceDistillationAdmissionPolicyTests
{
    /// <summary>
    /// 缺少 QA 对时应拒绝。
    /// </summary>
    [Fact]
    public void Decide_ShouldReject_WhenQaPairIsMissing()
    {
        var decision = ExperienceDistillationAdmissionPolicy.Decide("", "回答内容");

        decision.ShouldDistill.Should().BeFalse();
        decision.Reason.Should().Be(ExperienceDistillationAdmissionReasons.MissingQaPair);
    }

    /// <summary>
    /// 内容过短时应拒绝。
    /// </summary>
    [Fact]
    public void Decide_ShouldReject_WhenContentIsTooShort()
    {
        var decision = ExperienceDistillationAdmissionPolicy.Decide("短问题", "短回答");

        decision.ShouldDistill.Should().BeFalse();
        decision.Reason.Should().Be(ExperienceDistillationAdmissionReasons.ContentTooShort);
    }

    /// <summary>
    /// 缺少价值信号时应拒绝普通长回答。
    /// </summary>
    [Fact]
    public void Decide_ShouldReject_WhenValueSignalIsMissing()
    {
        var decision = ExperienceDistillationAdmissionPolicy.Decide(
            "请解释一下什么是异步编程以及它为什么有用。",
            "异步编程是一种允许程序在等待外部操作时继续执行其他工作的方式。它常用于网络请求、文件读写和后台任务，可以提高响应性。");

        decision.ShouldDistill.Should().BeFalse();
        decision.Reason.Should().Be(ExperienceDistillationAdmissionReasons.MissingValueSignal);
        decision.MatchedValueSignalKeyword.Should().BeEmpty();
        decision.MatchedSkipConditionKeyword.Should().BeEmpty();
    }

    /// <summary>
    /// 合格且包含长期价值信号的 QA 对应准入。
    /// </summary>
    [Fact]
    public void Decide_ShouldAccept_WhenQaPairContainsValueSignal()
    {
        var decision = ExperienceDistillationAdmissionPolicy.Decide(
            "后续这个项目的上下文治理应该怎么保持闭环？",
            "必须把上下文压力原因、记忆沉淀触发和任务编排快照统一到同一份结构化事实源，后续自我迭代只能消费这些事实，不从 Prompt 文本反推。");

        decision.ShouldDistill.Should().BeTrue();
        decision.Reason.Should().Be(ExperienceDistillationAdmissionReasons.Accepted);
        decision.MatchedValueSignalKeyword.Should().Be("闭环");
        decision.MatchedSkipConditionKeyword.Should().BeEmpty();
    }

    /// <summary>
    /// 准入策略应消费提纯协议中的高价值信号关键词，避免 Prompt 白名单与准入白名单漂移。
    /// </summary>
    [Fact]
    public void Decide_ShouldUseProtocolHighValueSignalKeywords()
    {
        var keyword = ExperienceDistillationOutputProtocol.HighValueSignalKeywords
            .First(item => string.Equals(item, "decision", StringComparison.Ordinal));
        var decision = ExperienceDistillationAdmissionPolicy.Decide(
            "What decision should remain for the next iteration?",
            $"The team made a long-term {keyword} to keep memory governance inside Core policies.");

        decision.ShouldDistill.Should().BeTrue();
        decision.Reason.Should().Be(ExperienceDistillationAdmissionReasons.Accepted);
        decision.MatchedValueSignalKeyword.Should().Be(keyword);
    }

    /// <summary>
    /// 命中协议跳过条件时应拒绝准入，即使文本中同时出现高价值关键词。
    /// </summary>
    [Fact]
    public void Decide_ShouldReject_WhenProtocolSkipConditionIsMatched()
    {
        var keyword = ExperienceDistillationOutputProtocol.SkipConditionKeywords
            .First(item => string.Equals(item, "运行测试", StringComparison.Ordinal));
        var decision = ExperienceDistillationAdmissionPolicy.Decide(
            $"这次只是{keyword}，不需要形成长期经验吗？",
            "虽然回答里出现架构、决策、流程这些词，但本轮只是执行一次测试命令并查看结果，不应沉淀为长期 SOP。");

        decision.ShouldDistill.Should().BeFalse();
        decision.Reason.Should().Be(ExperienceDistillationAdmissionReasons.SkipConditionMatched);
        decision.MatchedSkipConditionKeyword.Should().Be(keyword);
        decision.MatchedValueSignalKeyword.Should().BeEmpty();
    }
}
