using DevNexus.Core.Services.Swarm;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Swarm;

/// <summary>
/// Swarm 控制命令策略测试。
/// </summary>
public sealed class SwarmControlCommandPolicyTests
{
    /// <summary>
    /// 运行中或暂停中的会话允许恢复。
    /// </summary>
    [Theory]
    [InlineData(SwarmStatus.Running)]
    [InlineData(SwarmStatus.Paused)]
    public void CanResume_ShouldAccept_WhenSessionIsNotTerminal(SwarmStatus status)
    {
        var decision = SwarmControlCommandPolicy.CanResume(status);

        decision.Accepted.Should().BeTrue();
        decision.Command.Should().Be("Resumed");
    }

    /// <summary>
    /// 终态会话不允许伪恢复。
    /// </summary>
    [Theory]
    [InlineData(SwarmStatus.Completed)]
    [InlineData(SwarmStatus.Failed)]
    [InlineData(SwarmStatus.Aborted)]
    public void CanResume_ShouldReject_WhenSessionIsTerminal(SwarmStatus status)
    {
        var decision = SwarmControlCommandPolicy.CanResume(status);

        decision.Accepted.Should().BeFalse();
        decision.Command.Should().Be("ResumeRejected");
        decision.Message.Should().Be("Swarm 已经结束，无法继续。");
    }

    /// <summary>
    /// 缺失会话不允许暂停。
    /// </summary>
    [Fact]
    public void CanPause_ShouldReject_WhenSessionMissing()
    {
        var decision = SwarmControlCommandPolicy.CanPause(null);

        decision.Accepted.Should().BeFalse();
        decision.Command.Should().Be("PauseRejected");
        decision.Message.Should().Be("Swarm 会话不存在，无法暂停。");
    }

    /// <summary>
    /// 失败工作包允许进入重试流程。
    /// </summary>
    [Fact]
    public void CanRetryPackage_ShouldAccept_WhenPackageFailed()
    {
        var decision = SwarmControlCommandPolicy.CanRetryPackage(SwarmStatus.Failed, SwarmTaskStatus.Failed);

        decision.Accepted.Should().BeTrue();
        decision.Command.Should().Be("RetryStarted");
    }

    /// <summary>
    /// 终态中止会话不允许伪重试。
    /// </summary>
    [Fact]
    public void CanRetryPackage_ShouldReject_WhenSessionAborted()
    {
        var decision = SwarmControlCommandPolicy.CanRetryPackage(SwarmStatus.Aborted, SwarmTaskStatus.Failed);

        decision.Accepted.Should().BeFalse();
        decision.Command.Should().Be("RetryRejected");
        decision.Message.Should().Be("Swarm 已中止，无法重试工作包。");
    }

    /// <summary>
    /// 非失败工作包不允许重试。
    /// </summary>
    [Fact]
    public void CanRetryPackage_ShouldReject_WhenPackageIsNotFailed()
    {
        var decision = SwarmControlCommandPolicy.CanRetryPackage(SwarmStatus.Running, SwarmTaskStatus.Completed);

        decision.Accepted.Should().BeFalse();
        decision.Command.Should().Be("RetryRejected");
        decision.Message.Should().Be("仅允许重试失败工作包。");
    }
}
