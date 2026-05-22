using DevNexus.Core.Services.Cli;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Cli;

/// <summary>
/// CLI 终止结果构建器测试。
/// </summary>
public sealed class CliTerminationResultBuilderTests
{
    /// <summary>
    /// 会话缺失时应返回已结束结果。
    /// </summary>
    [Fact]
    public void BuildMissing_ShouldReturnAlreadyExitedWithoutState()
    {
        var sessionId = Guid.NewGuid();

        var result = CliTerminationResultBuilder.BuildMissing(sessionId);

        result.SessionId.Should().Be(sessionId);
        result.Terminated.Should().BeFalse();
        result.AlreadyExited.Should().BeTrue();
        result.State.Should().BeNull();
    }

    /// <summary>
    /// 会话已结束时应保留原始状态。
    /// </summary>
    [Fact]
    public void BuildAlreadyExited_ShouldKeepExistingState()
    {
        var state = CreateRunningState();
        state.IsActive = false;
        state.ExecStatus = CliExecStatus.Completed;
        state.SessionState = CliSessionState.Completed.ToWireValue();

        var result = CliTerminationResultBuilder.BuildAlreadyExited(state.SessionId, state);

        result.Terminated.Should().BeFalse();
        result.AlreadyExited.Should().BeTrue();
        result.State.Should().BeSameAs(state);
    }

    /// <summary>
    /// 终止成功时应生成取消态和低噪摘要。
    /// </summary>
    [Fact]
    public void BuildTerminated_ShouldCreateCancelledState()
    {
        var state = CreateRunningState();

        var result = CliTerminationResultBuilder.BuildTerminated(state.SessionId, state);

        result.Terminated.Should().BeTrue();
        result.AlreadyExited.Should().BeFalse();
        result.State.Should().NotBeNull();
        result.State!.ExecStatus.Should().Be(CliExecStatus.Cancelled);
        result.State.SessionState.Should().Be(CliSessionState.Cancelled.ToWireValue());
        result.State.TerminationReason.Should().Be(CliSessionTerminationReasons.Cancelled);
        result.State.WaitingForInput.Should().BeFalse();
        result.State.StatusSummary.Should().NotBeNull();
        result.State.StatusSummary!.Label.Should().Be("已停止");
    }

    /// <summary>
    /// 终止后的持久化实体应保留回读所需事实。
    /// </summary>
    [Fact]
    public void BuildPersistedSession_ShouldKeepCancelledFacts()
    {
        var userId = Guid.NewGuid();
        var terminatedState = CliTerminationResultBuilder
            .BuildTerminated(Guid.NewGuid(), CreateRunningState())
            .State!;

        var session = CliTerminationResultBuilder.BuildPersistedSession(userId, terminatedState);

        session.UserId.Should().Be(userId);
        session.ChatSessionId.Should().Be(terminatedState.SessionId);
        session.SessionKey.Should().Be(terminatedState.SessionKey);
        session.ExecStatus.Should().Be(CliExecStatus.Cancelled);
        session.Command.Should().Be(terminatedState.Command);
        session.TerminalStreamId.Should().Be(terminatedState.TerminalStreamId);
        session.TerminationReason.Should().Be(CliSessionTerminationReasons.Cancelled);
        session.IsActive.Should().BeFalse();
        session.WaitingForInput.Should().BeFalse();
    }

    private static CliSessionStateDto CreateRunningState()
    {
        return new CliSessionStateDto
        {
            SessionId = Guid.NewGuid(),
            ExecStatus = CliExecStatus.Running,
            SessionMode = CliSessionMode.InteractiveShell,
            SessionKey = "session-key",
            TerminalStreamId = Guid.NewGuid(),
            Command = "dotnet test",
            WorkingDirectory = "E:/zbg/DevNexus-AI",
            Status = TerminalStreamStatus.Running.ToWireValue(),
            SessionState = CliSessionState.Running.ToWireValue(),
            RuntimeHost = "process-cli",
            WaitingForInput = true,
            WaitingForInputSince = DateTime.UtcNow.AddSeconds(-10),
            StartedAt = DateTime.UtcNow.AddMinutes(-1),
            LastActivityAt = DateTime.UtcNow,
            TerminationReason = CliSessionTerminationReasons.None,
            IsActive = true
        };
    }
}
