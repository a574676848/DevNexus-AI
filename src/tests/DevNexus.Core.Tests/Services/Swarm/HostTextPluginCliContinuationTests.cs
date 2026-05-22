using DevNexus.Core.Abstractions;
using DevNexus.Core.Models.Cli;
using DevNexus.Core.Models.Execution;
using DevNexus.Core.Services.Swarm.Context;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Swarm;

/// <summary>
/// HostService 文本插件 CLI 续接测试。
/// </summary>
public sealed class HostTextPluginCliContinuationTests
{
    /// <summary>
    /// 缺少会话上下文时应返回失败标签，避免模型误以为已完成等待。
    /// </summary>
    [Fact]
    public async Task WaitCommandAsync_ShouldReturnFailure_WhenContextMissing()
    {
        var plugin = new HostTextPlugin(new FakeHostService());

        var result = await plugin.WaitCommandAsync();

        result.Should().StartWith("[FAILURE]");
        result.Should().Contain("缺少 CLI 会话上下文");
    }

    /// <summary>
    /// 活跃 CLI 会话继续运行时应返回信息态，提示模型继续等待或查看输出。
    /// </summary>
    [Fact]
    public async Task WaitCommandAsync_ShouldReturnInfo_WhenSessionStillActive()
    {
        var sessionId = Guid.NewGuid();
        var userContext = new FakeUserContextAccessor
        {
            CurrentUserId = Guid.NewGuid(),
            CurrentSessionId = sessionId.ToString()
        };
        var coordinator = new FakeCliRuntimeCoordinator
        {
            WaitSession = CreateSession(sessionId, CliExecStatus.Running, isActive: true)
        };
        var plugin = new HostTextPlugin(new FakeHostService(), coordinator, userContext);

        var result = await plugin.WaitCommandAsync(timeoutMs: 500);

        coordinator.LastWaitTimeout.Should().Be(TimeSpan.FromMilliseconds(1000));
        result.Should().StartWith("[INFO]");
        result.Should().Contain("status: Running");
        result.Should().Contain("recommendedTool: HostService.WaitCommandAsync");
        result.Should().Contain("outputTail:");
    }

    /// <summary>
    /// stdin 发送后会话终态应返回成功标签，便于模型继续总结结果。
    /// </summary>
    [Fact]
    public async Task SendCommandInputAsync_ShouldReturnSuccess_WhenInputCompletesSession()
    {
        var sessionId = Guid.NewGuid();
        var userContext = new FakeUserContextAccessor
        {
            CurrentUserId = Guid.NewGuid(),
            CurrentSessionId = sessionId.ToString()
        };
        var coordinator = new FakeCliRuntimeCoordinator
        {
            InputSession = CreateSession(sessionId, CliExecStatus.Completed, isActive: false)
        };
        var plugin = new HostTextPlugin(new FakeHostService(), coordinator, userContext);

        var result = await plugin.SendCommandInputAsync("y");

        coordinator.LastInput.Should().Be("y");
        result.Should().StartWith("[SUCCESS]");
        result.Should().Contain("已发送终端输入");
        result.Should().Contain("status: Completed");
        result.Should().Contain("recommendedTool: ReviewResult");
    }

    /// <summary>
    /// stdin 续接找不到会话时应返回失败标签，避免模型继续围绕伪会话重试。
    /// </summary>
    [Fact]
    public async Task SendCommandInputAsync_ShouldReturnFailure_WhenSessionMissing()
    {
        var sessionId = Guid.NewGuid();
        var userContext = new FakeUserContextAccessor
        {
            CurrentUserId = Guid.NewGuid(),
            CurrentSessionId = sessionId.ToString()
        };
        var coordinator = new FakeCliRuntimeCoordinator();
        var plugin = new HostTextPlugin(new FakeHostService(), coordinator, userContext);

        var result = await plugin.SendCommandInputAsync("y");

        result.Should().StartWith("[FAILURE]");
        result.Should().Contain("未找到终端会话");
        coordinator.LastInput.Should().Be("y");
    }

    /// <summary>
    /// 缺少会话上下文时停止命令应返回失败标签。
    /// </summary>
    [Fact]
    public async Task StopCommandAsync_ShouldReturnFailure_WhenContextMissing()
    {
        var plugin = new HostTextPlugin(new FakeHostService());

        var result = await plugin.StopCommandAsync();

        result.Should().StartWith("[FAILURE]");
        result.Should().Contain("缺少 CLI 会话上下文");
    }

    /// <summary>
    /// 停止当前 CLI 会话应通过协调器终止同一会话。
    /// </summary>
    [Fact]
    public async Task StopCommandAsync_ShouldTerminateCurrentSession()
    {
        var sessionId = Guid.NewGuid();
        var userContext = new FakeUserContextAccessor
        {
            CurrentUserId = Guid.NewGuid(),
            CurrentSessionId = sessionId.ToString()
        };
        var coordinator = new FakeCliRuntimeCoordinator
        {
            TerminationResult = CreateTerminationResult(sessionId, terminated: true)
        };
        var plugin = new HostTextPlugin(new FakeHostService(), coordinator, userContext);

        var result = await plugin.StopCommandAsync();

        coordinator.TerminatedSessionId.Should().Be(sessionId);
        result.Should().StartWith("[SUCCESS]");
        result.Should().Contain("terminated: True");
        result.Should().Contain("status: Cancelled");
        result.Should().Contain("recommendedTool: ReviewResult");
    }

    /// <summary>
    /// 停止命令找不到会话时应返回失败标签，避免模型误判为已终止活跃命令。
    /// </summary>
    [Fact]
    public async Task StopCommandAsync_ShouldReturnFailure_WhenSessionMissing()
    {
        var sessionId = Guid.NewGuid();
        var userContext = new FakeUserContextAccessor
        {
            CurrentUserId = Guid.NewGuid(),
            CurrentSessionId = sessionId.ToString()
        };
        var coordinator = new FakeCliRuntimeCoordinator
        {
            TerminationResult = CreateMissingTerminationResult(sessionId)
        };
        var plugin = new HostTextPlugin(new FakeHostService(), coordinator, userContext);

        var result = await plugin.StopCommandAsync();

        result.Should().StartWith("[FAILURE]");
        result.Should().Contain("alreadyExited: True");
        result.Should().Contain("recommendedTool: ReviewResult");
    }

    /// <summary>
    /// 等待输入的 CLI 会话应明确推荐 stdin 续接工具。
    /// </summary>
    [Fact]
    public async Task WaitCommandAsync_ShouldRecommendSendInput_WhenSessionWaitsForInput()
    {
        var sessionId = Guid.NewGuid();
        var userContext = new FakeUserContextAccessor
        {
            CurrentUserId = Guid.NewGuid(),
            CurrentSessionId = sessionId.ToString()
        };
        var coordinator = new FakeCliRuntimeCoordinator
        {
            WaitSession = CreateSession(sessionId, CliExecStatus.WaitingForInput, isActive: true, waitingForInput: true)
        };
        var plugin = new HostTextPlugin(new FakeHostService(), coordinator, userContext);

        var result = await plugin.WaitCommandAsync();

        result.Should().StartWith("[INFO]");
        result.Should().Contain("waitingForInput: True");
        result.Should().Contain("recommendedTool: HostService.SendCommandInputAsync");
    }

    private static CliExecSessionDto CreateSession(
        Guid sessionId,
        CliExecStatus status,
        bool isActive,
        bool waitingForInput = false)
    {
        return new CliExecSessionDto
        {
            SessionId = sessionId,
            OutputTail = "ready",
            OutputLength = 5,
            Exited = !isActive,
            State = new CliSessionStateDto
            {
                SessionId = sessionId,
                ExecStatus = status,
                SessionMode = CliSessionMode.InteractiveShell,
                SessionKey = "session-key",
                IsActive = isActive,
                WaitingForInput = waitingForInput,
                StatusSummary = new CliRuntimeStatusSummaryDto
                {
                    Label = waitingForInput ? "等待输入" : isActive ? "运行中" : "已完成",
                    Description = waitingForInput
                        ? "终端正在等待输入。"
                        : isActive ? "终端仍在运行，可随时查看。" : "终端命令已完成，可查看输出结果。",
                    NextAction = waitingForInput ? "SendInput" : isActive ? "WatchOutput" : "ReviewResult",
                    IsTerminal = !isActive
                }
            }
        };
    }

    private static CliExecTerminateResultDto CreateMissingTerminationResult(Guid sessionId)
    {
        return new CliExecTerminateResultDto
        {
            SessionId = sessionId,
            Terminated = false,
            AlreadyExited = true,
            Message = "当前终端会话不存在或已结束。"
        };
    }

    private static CliExecTerminateResultDto CreateTerminationResult(Guid sessionId, bool terminated)
    {
        return new CliExecTerminateResultDto
        {
            SessionId = sessionId,
            Terminated = terminated,
            AlreadyExited = !terminated,
            Message = terminated ? "已停止当前终端会话。" : "当前终端会话已结束。",
            State = new CliSessionStateDto
            {
                SessionId = sessionId,
                ExecStatus = CliExecStatus.Cancelled,
                SessionMode = CliSessionMode.InteractiveShell,
                SessionKey = "session-key",
                IsActive = false,
                WaitingForInput = false,
                StatusSummary = new CliRuntimeStatusSummaryDto
                {
                    Label = "已取消",
                    Description = "终端命令已停止。",
                    NextAction = "ReviewResult",
                    IsTerminal = true
                }
            }
        };
    }

    private sealed class FakeUserContextAccessor : IUserContextAccessor
    {
        public Guid? CurrentUserId { get; set; }

        public string? CurrentSessionId { get; set; }

        public string? CurrentConnectionId { get; set; }
    }

    private sealed class FakeCliRuntimeCoordinator : ICliRuntimeCoordinator
    {
        public CliExecSessionDto? WaitSession { get; set; }

        public CliExecSessionDto? InputSession { get; set; }

        public CliExecTerminateResultDto? TerminationResult { get; set; }

        public TimeSpan LastWaitTimeout { get; private set; }

        public string? LastInput { get; private set; }

        public Guid? TerminatedSessionId { get; private set; }

        public Task<CliExecSessionDto?> GetSessionAsync(
            Guid userId,
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<CliExecSessionDto?>(WaitSession ?? InputSession);
        }

        public Task<CliExecLogChunkDto?> GetLogChunkAsync(
            Guid userId,
            Guid sessionId,
            int startIndex = 0,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<CliExecLogChunkDto?>(null);
        }

        public Task<CliExecSessionDto?> WaitForExitAsync(
            Guid userId,
            Guid sessionId,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            LastWaitTimeout = timeout;
            return Task.FromResult(WaitSession);
        }

        public Task<CliExecSessionDto> WriteInputAsync(
            Guid userId,
            Guid sessionId,
            string input,
            CancellationToken cancellationToken = default)
        {
            LastInput = input;
            return Task.FromResult(InputSession!);
        }

        public Task<CliExecTerminateResultDto> TerminateAsync(
            Guid userId,
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            TerminatedSessionId = sessionId;
            return Task.FromResult(TerminationResult ?? new CliExecTerminateResultDto { SessionId = sessionId });
        }

        public Task<CliExecRollbackResultDto> RollbackAsync(
            Guid userId,
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CliExecRollbackResultDto { SessionId = sessionId });
        }

        public Task<CliSessionRuntimeSnapshot?> GetRuntimeSnapshotAsync(
            Guid userId,
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<CliSessionRuntimeSnapshot?>(null);
        }

        public Task<CliExecCheckpointDto?> GetLatestCheckpointAsync(
            Guid userId,
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<CliExecCheckpointDto?>(null);
        }
    }

    private sealed class FakeHostService : IHostStructuredService
    {
        public bool ValidatePathAccess(string path) => true;

        public Task<HostCommandExecutionResult> ExecuteCommandResultAsync(
            string command,
            string arguments,
            string workingDirectory,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HostCommandExecutionResult());
        }

        public Task<HostTextOperationResult> ReadFileTextResultAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HostTextOperationResult());
        }

        public Task<HostOperationResult> WriteFileTextResultAsync(
            string path,
            string content,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HostOperationResult());
        }

        public Task<HostTextOperationResult> ListDirectoryResultAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HostTextOperationResult());
        }

        public Task<HostTextOperationResult> SearchInFilesResultAsync(
            string directory,
            string query,
            string filePattern = "*",
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HostTextOperationResult());
        }

        public Task<HostFileListOperationResult> ListFilesRecursiveResultAsync(
            string path,
            string[] patterns,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HostFileListOperationResult());
        }

        public Task<HostOperationResult> ApplyDiffResultAsync(
            string path,
            string originalContent,
            string newContent,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HostOperationResult());
        }
    }
}
