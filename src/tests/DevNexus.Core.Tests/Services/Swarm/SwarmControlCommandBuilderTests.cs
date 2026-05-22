using DevNexus.Core.Services.Swarm;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Swarm;

/// <summary>
/// Swarm 控制命令构建器测试。
/// </summary>
public sealed class SwarmControlCommandBuilderTests
{
    /// <summary>
    /// 暂停命令应携带暂停后的摘要。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnPausedSummary_WhenPaused()
    {
        var sessionId = Guid.NewGuid().ToString();

        var command = SwarmControlCommandBuilder.Build(
            sessionId,
            "Paused",
            new[] { CreatePackage(SwarmTaskStatus.Pending) },
            isPaused: true);

        command.SessionId.Should().Be(sessionId);
        command.Command.Should().Be("Paused");
        command.Accepted.Should().BeTrue();
        command.Message.Should().Be("Swarm 已暂停。");
        command.StatusSummary.Should().NotBeNull();
        command.StatusSummary!.IsPaused.Should().BeTrue();
        command.StatusSummary.Label.Should().Be("Swarm 已暂停");
    }

    /// <summary>
    /// 恢复命令应携带当前执行摘要。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnRunningSummary_WhenResumed()
    {
        var command = SwarmControlCommandBuilder.Build(
            Guid.NewGuid().ToString(),
            "Resumed",
            new[] { CreatePackage(SwarmTaskStatus.InProgress) },
            isPaused: false);

        command.Command.Should().Be("Resumed");
        command.Accepted.Should().BeTrue();
        command.StatusSummary.Should().NotBeNull();
        command.StatusSummary!.IsPaused.Should().BeFalse();
        command.StatusSummary.Tone.Should().Be("active");
        command.StatusSummary.ExecutingCount.Should().Be(1);
    }

    /// <summary>
    /// 被拒绝的控制命令应携带拒绝说明和当前摘要。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnRejectedCommand()
    {
        var command = SwarmControlCommandBuilder.Build(
            Guid.NewGuid().ToString(),
            "ResumeRejected",
            new[] { CreatePackage(SwarmTaskStatus.Completed) },
            isPaused: false,
            accepted: false,
            message: "Swarm 已经结束，无法继续。");

        command.Command.Should().Be("ResumeRejected");
        command.Accepted.Should().BeFalse();
        command.Message.Should().Be("Swarm 已经结束，无法继续。");
        command.StatusSummary.Should().NotBeNull();
        command.StatusSummary!.IsTerminal.Should().BeTrue();
    }

    /// <summary>
    /// 重试命令应使用产品化提示，并携带当前工作包摘要。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnRetryStartedCommand()
    {
        var command = SwarmControlCommandBuilder.Build(
            Guid.NewGuid().ToString(),
            "RetryStarted",
            new[] { CreatePackage(SwarmTaskStatus.Retrying) },
            isPaused: false);

        command.Command.Should().Be("RetryStarted");
        command.Accepted.Should().BeTrue();
        command.Message.Should().Be("工作包重试已开始。");
        command.StatusSummary.Should().NotBeNull();
        command.StatusSummary!.IsPaused.Should().BeFalse();
        command.StatusSummary.EvaluatingCount.Should().Be(1);
    }

    private static ContextWorkPackageRecord CreatePackage(SwarmTaskStatus status)
    {
        return new ContextWorkPackageRecord
        {
            TaskId = Guid.NewGuid().ToString("N"),
            Title = "工作包",
            Description = "执行工作包",
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
