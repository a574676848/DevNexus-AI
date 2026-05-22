using DevNexus.Core.Services.Cli;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Cli;

/// <summary>
/// CLI 回滚结果构建器测试。
/// </summary>
public sealed class CliRollbackResultBuilderTests
{
    /// <summary>
    /// 运行中的会话应阻断回滚并保留当前状态。
    /// </summary>
    [Fact]
    public void BuildBlockedByActiveSession_ShouldKeepActiveState()
    {
        var state = CreateRunningState();

        var result = CliRollbackResultBuilder.BuildBlockedByActiveSession(state.SessionId, state);

        result.SessionId.Should().Be(state.SessionId);
        result.RolledBack.Should().BeFalse();
        result.WorkingDirectory.Should().Be(state.WorkingDirectory);
        result.State.Should().BeSameAs(state);
    }

    /// <summary>
    /// 回滚成功时应生成终态会话快照。
    /// </summary>
    [Fact]
    public void BuildRolledBack_ShouldCreateRolledBackState()
    {
        var sessionId = Guid.NewGuid();
        var sessionKey = "session-key";
        var checkpointResult = new CliExecRollbackResultDto
        {
            SessionId = sessionId,
            RolledBack = true,
            Message = "已回滚到最近快照。",
            WorkingDirectory = "E:/zbg/DevNexus-AI"
        };
        var existing = CreatePersistedSession(sessionId, sessionKey);

        var result = CliRollbackResultBuilder.BuildRolledBack(
            sessionId,
            sessionKey,
            checkpointResult,
            existing);

        result.RolledBack.Should().BeTrue();
        result.State.Should().NotBeNull();
        result.State!.ExecStatus.Should().Be(CliExecStatus.RolledBack);
        result.State.SessionState.Should().Be(CliSessionState.RolledBack.ToWireValue());
        result.State.Status.Should().Be(TerminalStreamStatus.Completed.ToWireValue());
        result.State.TerminationReason.Should().Be(CliSessionTerminationReasons.Completed);
        result.State.IsActive.Should().BeFalse();
        result.State.WaitingForInput.Should().BeFalse();
        result.State.StatusSummary.Should().NotBeNull();
        result.State.StatusSummary!.Label.Should().Be("已回滚");
    }

    /// <summary>
    /// 回滚后的持久化实体应保留回读所需事实。
    /// </summary>
    [Fact]
    public void BuildPersistedSession_ShouldKeepRolledBackFacts()
    {
        var userId = Guid.NewGuid();
        var state = CliRollbackResultBuilder
            .BuildRolledBack(
                Guid.NewGuid(),
                "session-key",
                new CliExecRollbackResultDto
                {
                    RolledBack = true,
                    Message = "已回滚。",
                    WorkingDirectory = "E:/zbg/DevNexus-AI"
                },
                CreatePersistedSession(Guid.NewGuid(), "session-key"))
            .State!;

        var session = CliRollbackResultBuilder.BuildPersistedSession(userId, state);

        session.UserId.Should().Be(userId);
        session.ChatSessionId.Should().Be(state.SessionId);
        session.SessionKey.Should().Be(state.SessionKey);
        session.ExecStatus.Should().Be(CliExecStatus.RolledBack);
        session.TerminationReason.Should().Be(CliSessionTerminationReasons.Completed);
        session.IsActive.Should().BeFalse();
        session.WaitingForInput.Should().BeFalse();
        session.ExitCode.Should().BeNull();
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
            WaitingForInput = false,
            StartedAt = DateTime.UtcNow.AddMinutes(-1),
            LastActivityAt = DateTime.UtcNow,
            TerminationReason = CliSessionTerminationReasons.None,
            IsActive = true
        };
    }

    private static CliExecSession CreatePersistedSession(Guid sessionId, string sessionKey)
    {
        return new CliExecSession
        {
            SessionKey = sessionKey,
            UserId = Guid.NewGuid(),
            ChatSessionId = sessionId,
            ExecStatus = CliExecStatus.Completed,
            SessionMode = CliSessionMode.InteractiveShell,
            Command = "dotnet test",
            WorkingDirectory = "E:/zbg/DevNexus-AI",
            RuntimeHost = "process-cli",
            TerminalStreamId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddMinutes(-2),
            LastActivityAt = DateTime.UtcNow.AddMinutes(-1),
            WaitingForInput = true,
            ExitCode = 1,
            TerminationReason = CliSessionTerminationReasons.Completed,
            IsActive = false
        };
    }
}
