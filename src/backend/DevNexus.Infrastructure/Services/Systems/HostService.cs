using DevNexus.Core.Abstractions;
using DevNexus.Core.Models.Cli;
using DevNexus.Core.Models.Execution;
using DevNexus.Core.Models.Evaluation;
using DevNexus.Core.Services.Chat;
using DevNexus.Core.Services.Swarm.Execution;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Constants;
using DevNexus.Shared.Enums;
using DevNexus.Shared.DTOs;
using DevNexus.Infrastructure.Services.CliTerminal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Linq;

namespace DevNexus.Infrastructure.Services.Systems;

/// <summary>
/// 宿主服务，负责安全执行命令并提供受控文件访问能力。
/// </summary>
public partial class HostService : IHostStructuredService, ICliExecService
{
    private readonly ILogger<HostService> _logger;
    private readonly IUserContextAccessor _userContextAccessor;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IRuntimeEventNotifier _runtimeEventNotifier;
    private readonly ICliSandboxWarmPool _cliSandboxWarmPool;
    private readonly CliSessionManager _sessionManager;
    private readonly ITerminalNotifier _terminalNotifier;

    public HostService(
        ILogger<HostService> logger,
        IUserContextAccessor userContextAccessor,
        IServiceScopeFactory serviceScopeFactory,
        IRuntimeEventNotifier runtimeEventNotifier,
        ICliSandboxWarmPool cliSandboxWarmPool,
        ITerminalNotifier terminalNotifier,
        CliSessionManager sessionManager)
    {
        _logger = logger;
        _userContextAccessor = userContextAccessor;
        _serviceScopeFactory = serviceScopeFactory;
        _runtimeEventNotifier = runtimeEventNotifier;
        _cliSandboxWarmPool = cliSandboxWarmPool;
        _terminalNotifier = terminalNotifier;
        _sessionManager = sessionManager;
    }

    /// <inheritdoc />
    public async Task<HostCommandExecutionResult> ExecuteCommandResultAsync(
        string command,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        command = (command ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(command))
        {
            if (!string.IsNullOrWhiteSpace(arguments))
            {
                // 兼容模型把完整命令误塞进 arguments 的场景，避免 SK 因必填参数缺失直接让整次终端执行失败。
                command = arguments.Trim();
                arguments = string.Empty;
            }
            else
            {
                return new HostCommandExecutionResult
                {
                    Status = HostOperationStatus.Failure,
                    Message = "缺少 command 参数。请提供完整命令，或将完整命令写入 command 字段。"
                };
            }
        }

        var publicSessionId = string.IsNullOrWhiteSpace(_userContextAccessor.CurrentSessionId)
            ? Guid.NewGuid().ToString()
            : _userContextAccessor.CurrentSessionId!;

        // userId/sessionId 仍用于会话隔离、审批和审计，不再用于限制本机目录边界。
        var userId = _userContextAccessor.CurrentUserId;
        if (!userId.HasValue)
        {
            return new HostCommandExecutionResult
            {
                Status = HostOperationStatus.Failure,
                Message = "缺少用户上下文，无法执行宿主命令。"
            };
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var cliExecutionPolicyService = scope.ServiceProvider.GetRequiredService<ICliExecutionPolicyService>();
        var pendingInteractionService = scope.ServiceProvider.GetRequiredService<IPendingInteractionService>();
        var cliExecCheckpointService = scope.ServiceProvider.GetRequiredService<ICliExecCheckpointService>();
        var cliExecSessionRepository = scope.ServiceProvider.GetRequiredService<ICliExecSessionRepository>();

        var contextSnapshot = ChatExecutionContext.GetSnapshot();
        var targetWd = cliExecutionPolicyService.ResolveWorkingDirectory(userId.Value, workingDirectory);
        var policy = await cliExecutionPolicyService.EvaluateCommandAsync(
            userId.Value,
            publicSessionId,
            command,
            arguments,
            targetWd,
            contextSnapshot.ApprovalMode,
            cancellationToken);
        if (!policy.Allowed)
        {
            if (Guid.TryParse(publicSessionId, out var parsedSessionId))
            {
                var internalSessionKey = BuildInternalCliSessionKey(userId.Value, publicSessionId);
                var cliEventData = new CliExecApprovalRequestDto
                {
                    SessionId = parsedSessionId,
                    SessionKey = internalSessionKey,
                    Command = string.IsNullOrWhiteSpace(arguments) ? command : $"{command} {arguments}",
                    WorkingDirectory = targetWd,
                    Status = CliApprovalStatus.Pending,
                    FailureReason = policy.FailureReason,
                    SuggestedAction = policy.SuggestedAction,
                    Message = policy.Message
                };

                if (policy.RequiresHumanIntervention)
                {
                    await PersistPendingApprovalSessionAsync(
                        userId.Value,
                        parsedSessionId,
                        internalSessionKey,
                        command,
                        arguments,
                        targetWd,
                        cliExecSessionRepository,
                        cancellationToken);

                    var toolRecord = new ToolExecutionRecord
                    {
                        ToolName = "HostService.ExecuteCommandResultAsync",
                        Arguments = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            command,
                            arguments,
                            workingDirectory = targetWd,
                            commandFingerprint = policy.CommandFingerprint,
                            commandPattern = policy.CommandPattern
                        }),
                        Success = false,
                        FailureReason = policy.FailureReason,
                        Retryable = false,
                        RequiresHumanIntervention = true,
                        ShouldFallback = false,
                        ShouldRotateCredential = false,
                        SuggestedAction = policy.SuggestedAction,
                        UserMessage = policy.Message,
                        ErrorSummary = policy.Message
                    };

                    var interaction = await pendingInteractionService.CreateOrReuseAsync(
                        parsedSessionId,
                        contextSnapshot.MessageId == Guid.Empty ? null : contextSnapshot.MessageId,
                        toolRecord,
                        evaluationFeedback: null,
                        cancellationToken);

                    await _runtimeEventNotifier.NotifyAsync(
                        userId.Value,
                        parsedSessionId,
                        ServerEventType.PendingInteractionCreated,
                        new
                        {
                            InteractionId = interaction.Id,
                            Kind = interaction.Kind.ToWireValue(),
                            interaction.Title,
                            interaction.Description
                        },
                        cancellationToken);

                    cliEventData = new CliExecApprovalRequestDto
                    {
                        SessionId = parsedSessionId,
                        InteractionId = interaction.Id,
                        SessionKey = internalSessionKey,
                        Command = string.IsNullOrWhiteSpace(arguments) ? command : $"{command} {arguments}",
                        WorkingDirectory = targetWd,
                        Status = CliApprovalStatus.Pending,
                        FailureReason = policy.FailureReason,
                        SuggestedAction = policy.SuggestedAction,
                        Message = policy.Message
                    };
                }

                await _runtimeEventNotifier.NotifyAsync(
                    userId.Value,
                    parsedSessionId,
                    policy.RequiresHumanIntervention
                        ? ServerEventType.CliExecApprovalRequired
                        : ServerEventType.CliExecRejected,
                    cliEventData,
                    cancellationToken);
            }

            return new HostCommandExecutionResult
            {
                Status = policy.RequiresHumanIntervention
                    ? HostOperationStatus.SecurityBlocked
                    : HostOperationStatus.Failure,
                Message = policy.Message,
                FailureReason = policy.FailureReason,
                SuggestedAction = policy.SuggestedAction,
                RequiresHumanIntervention = policy.RequiresHumanIntervention
            };
        }

        targetWd = policy.EffectiveWorkingDirectory ?? targetWd;

        _logger.LogInformation("[HostService] 执行命令 (Session={Session}): {Cmd} {Args}", publicSessionId, command, arguments);

        // 如果存在已活跃的持久化会话，使用哨兵机制执行并截获
        var fullCommand = string.IsNullOrWhiteSpace(arguments) ? command : $"{command} {arguments}";

        try
        {
            // 确保会话已初始化
            var sessionId = BuildInternalCliSessionKey(userId.Value, publicSessionId);
            var lockKey = BuildCliLockKey(userId.Value, targetWd);

            await cliExecCheckpointService.CreateCheckpointIfNeededAsync(
                userId.Value,
                Guid.TryParse(publicSessionId, out var parsedCheckpointSessionId) ? parsedCheckpointSessionId : null,
                sessionId,
                string.IsNullOrWhiteSpace(arguments) ? command : $"{command} {arguments}",
                targetWd,
                cancellationToken);

            await _cliSandboxWarmPool.WarmAsync(targetWd, cancellationToken);
            await _sessionManager.CreateSessionAsync(
                sessionId,
                targetWd,
                cancellationToken);

            // 订阅实时输出流 (Phase 7)
            var effectiveUserId = userId.Value;

            // 【AsyncLocal 降低依赖】在方法入口捕获上下文快照到局部变量，
            // 确保 HandleOutput 回调通过闭包使用稳定的局部变量，而非直接读取可能失效的上下文
            var messageId = contextSnapshot.MessageId;
            var attemptNumber = contextSnapshot.AttemptNumber;
            var parsedSessionId = Guid.TryParse(publicSessionId, out var sid) ? sid : Guid.Empty;

            var terminalStreamId = Guid.NewGuid();
            var toolCallId = contextSnapshot.ToolCallId;

            await cliExecSessionRepository.UpsertAsync(
                new CliExecSession
                {
                    SessionKey = sessionId,
                    UserId = effectiveUserId,
                    ChatSessionId = parsedSessionId == Guid.Empty ? null : parsedSessionId,
                    ExecStatus = CliExecStatus.Queued,
                    SessionMode = CliSessionMode.InteractiveShell,
                    Command = fullCommand,
                    WorkingDirectory = targetWd,
                    RuntimeHost = "process-cli",
                    TerminalStreamId = terminalStreamId,
                    StartedAt = DateTime.UtcNow,
                    LastActivityAt = DateTime.UtcNow,
                    WaitingForInput = false,
                    WaitingForInputSince = null,
                    TerminationReason = CliSessionTerminationReasons.None,
                    IsActive = true
                },
                cancellationToken);

            void HandleOutput(string sid, string delta)
            {
                if (sid == sessionId)
                {
                    if (parsedSessionId == Guid.Empty)
                    {
                        return;
                    }

                    var metadata = new Dictionary<string, object>
                    {
                        [TerminalBlockMetadataKeys.MessageId] = messageId,
                        [TerminalBlockMetadataKeys.SessionKey] = sessionId,
                        [TerminalBlockMetadataKeys.ChatSessionId] = parsedSessionId,
                        [TerminalBlockMetadataKeys.UserId] = effectiveUserId,
                        [TerminalBlockMetadataKeys.Command] = fullCommand,
                        [TerminalBlockMetadataKeys.WorkingDirectory] = targetWd,
                        [TerminalBlockMetadataKeys.LockKey] = lockKey,
                        [TerminalBlockMetadataKeys.Status] = TerminalStreamStatus.Running.ToWireValue(),
                        [TerminalBlockMetadataKeys.SessionState] = CliSessionState.Running.ToWireValue(),
                        [TerminalBlockMetadataKeys.RuntimeHost] = "process-cli",
                        [TerminalBlockMetadataKeys.AttemptNumber] = attemptNumber,
                        [TerminalBlockMetadataKeys.IsRetry] = attemptNumber > 0,
                        [TerminalBlockMetadataKeys.StartedAt] = DateTime.UtcNow,
                        [TerminalBlockMetadataKeys.LastActivityAt] = DateTime.UtcNow,
                        [TerminalBlockMetadataKeys.WaitingForInput] = false,
                        [TerminalBlockMetadataKeys.TerminationReason] = CliSessionTerminationReasons.None,
                        [TerminalBlockMetadataKeys.TerminalStreamId] = terminalStreamId
                    };

                    var runtimeSnapshot = _sessionManager.GetRuntimeSnapshot(sessionId);
                    if (runtimeSnapshot != null)
                    {
                        metadata[TerminalBlockMetadataKeys.LockKey] = runtimeSnapshot.LockKey;
                        metadata[TerminalBlockMetadataKeys.LastActivityAt] = runtimeSnapshot.LastActivityAt;
                        metadata[TerminalBlockMetadataKeys.WaitingForInput] = runtimeSnapshot.WaitingForInput;
                        metadata[TerminalBlockMetadataKeys.SessionState] = CliSessionStateExtensions
                            .Parse(runtimeSnapshot.State.ToString())
                            .ToWireValue();
                        metadata[TerminalBlockMetadataKeys.TerminationReason] = CliSessionTerminationReasons.Normalize(runtimeSnapshot.TerminationReason.ToString());

                        if (runtimeSnapshot.WaitingForInputSince.HasValue)
                        {
                            metadata[TerminalBlockMetadataKeys.WaitingForInputSince] = runtimeSnapshot.WaitingForInputSince.Value.ToString("O");
                        }
                    }

                    if (toolCallId != Guid.Empty)
                    {
                        metadata[TerminalBlockMetadataKeys.ToolCallId] = toolCallId;
                    }

                    if (SwarmExecutionContext.HasActive)
                    {
                        metadata[TerminalBlockMetadataKeys.PackageId] = SwarmExecutionContext.CurrentPackageId;
                    }

                    _ = _terminalNotifier.NotifyTerminalOutputAsync(
                        effectiveUserId,
                        parsedSessionId,
                        messageId,
                        delta,
                        false,
                        metadata);
                }
            }

            _sessionManager.OnOutputReceived += HandleOutput;

            try
            {
                // 使用 5 分钟默认超时执行
                var executionResult = await _sessionManager.ExecuteAndWaitAsync(
                    sessionId,
                    fullCommand,
                    TimeSpan.FromMinutes(5),
                    cancellationToken);
                var output = executionResult.Output;
                var exitCode = executionResult.ExitCode;
                var commandFinished = executionResult.State is CliCommandExecutionState.Completed
                    or CliCommandExecutionState.Failed
                    or CliCommandExecutionState.Cancelled
                    or CliCommandExecutionState.ProcessUnavailable;

                // 发送结束标识
                if (parsedSessionId != Guid.Empty)
                {
                    var metadata = new Dictionary<string, object>
                    {
                        [TerminalBlockMetadataKeys.MessageId] = messageId,
                        [TerminalBlockMetadataKeys.SessionKey] = sessionId,
                        [TerminalBlockMetadataKeys.ChatSessionId] = parsedSessionId,
                        [TerminalBlockMetadataKeys.UserId] = effectiveUserId,
                        [TerminalBlockMetadataKeys.Command] = fullCommand,
                        [TerminalBlockMetadataKeys.WorkingDirectory] = targetWd,
                        [TerminalBlockMetadataKeys.LockKey] = lockKey,
                        [TerminalBlockMetadataKeys.Status] = ResolveTerminalStatus(executionResult),
                        [TerminalBlockMetadataKeys.SessionState] = ResolveCliSessionState(executionResult),
                        [TerminalBlockMetadataKeys.RuntimeHost] = "process-cli",
                        [TerminalBlockMetadataKeys.ExitCode] = exitCode,
                        [TerminalBlockMetadataKeys.AttemptNumber] = attemptNumber,
                        [TerminalBlockMetadataKeys.IsRetry] = attemptNumber > 0,
                        [TerminalBlockMetadataKeys.StartedAt] = DateTime.UtcNow,
                        [TerminalBlockMetadataKeys.LastActivityAt] = DateTime.UtcNow,
                        [TerminalBlockMetadataKeys.WaitingForInput] = false,
                        [TerminalBlockMetadataKeys.TerminationReason] = ResolveTerminationReason(executionResult),
                        [TerminalBlockMetadataKeys.TerminalStreamId] = terminalStreamId
                    };

                    var runtimeSnapshot = _sessionManager.GetRuntimeSnapshot(sessionId);
                    if (runtimeSnapshot != null)
                    {
                        metadata[TerminalBlockMetadataKeys.StartedAt] = runtimeSnapshot.StartedAt.ToString("O");
                        metadata[TerminalBlockMetadataKeys.LastActivityAt] = runtimeSnapshot.LastActivityAt.ToString("O");
                        metadata[TerminalBlockMetadataKeys.WaitingForInput] = runtimeSnapshot.WaitingForInput;
                        metadata[TerminalBlockMetadataKeys.SessionState] = CliSessionStateExtensions
                            .Parse(runtimeSnapshot.State.ToString())
                            .ToWireValue();
                        metadata[TerminalBlockMetadataKeys.TerminationReason] = CliSessionTerminationReasons.Normalize(runtimeSnapshot.TerminationReason.ToString());

                        if (runtimeSnapshot.WaitingForInputSince.HasValue)
                        {
                            metadata[TerminalBlockMetadataKeys.WaitingForInputSince] = runtimeSnapshot.WaitingForInputSince.Value.ToString("O");
                        }
                    }

                    if (toolCallId != Guid.Empty)
                    {
                        metadata[TerminalBlockMetadataKeys.ToolCallId] = toolCallId;
                    }

                    if (SwarmExecutionContext.HasActive)
                    {
                        metadata[TerminalBlockMetadataKeys.PackageId] = SwarmExecutionContext.CurrentPackageId;
                    }

                    await _terminalNotifier.NotifyTerminalOutputAsync(
                        effectiveUserId,
                        parsedSessionId,
                        messageId,
                        "",
                        commandFinished,
                        metadata);
                }

                if (executionResult.State == CliCommandExecutionState.StillRunning)
                {
                    return new HostCommandExecutionResult
                    {
                        Status = HostOperationStatus.Info,
                        Message = "命令仍在运行，已保留终端会话用于继续查看输出。",
                        Output = output,
                        ExitCode = exitCode,
                        SuggestedAction = ToolSuggestedAction.WaitForCompletion
                    };
                }

                if (exitCode != 0)
                {
                    return new HostCommandExecutionResult
                    {
                        Status = HostOperationStatus.Failure,
                        Message = $"退出码: {exitCode}",
                        Output = output,
                        ExitCode = exitCode
                    };
                }

                return new HostCommandExecutionResult
                {
                    Status = HostOperationStatus.Success,
                    Message = "命令执行成功。",
                    Output = output,
                    ExitCode = exitCode
                };
            }
            finally
            {
                _sessionManager.OnOutputReceived -= HandleOutput;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HostService] 执行命令异常 | Session={Session}", publicSessionId);
            return new HostCommandExecutionResult
            {
                Status = HostOperationStatus.Exception,
                Message = $"执行命令异常：{ex.Message}",
                Output = ex.ToString()
            };
        }
    }

    private async Task PersistPendingApprovalSessionAsync(
        Guid userId,
        Guid chatSessionId,
        string sessionKey,
        string command,
        string arguments,
        string workingDirectory,
        ICliExecSessionRepository cliExecSessionRepository,
        CancellationToken cancellationToken)
    {
        await cliExecSessionRepository.UpsertAsync(
            new CliExecSession
            {
                UserId = userId,
                ChatSessionId = chatSessionId,
                SessionKey = sessionKey,
                ExecStatus = CliExecStatus.PendingApproval,
                SessionMode = CliSessionMode.OneShotCommand,
                Command = string.IsNullOrWhiteSpace(arguments) ? command : $"{command} {arguments}",
                WorkingDirectory = workingDirectory,
                RuntimeHost = "process-cli",
                StartedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                WaitingForInput = false,
                WaitingForInputSince = null,
                TerminationReason = "ApprovalRequired",
                IsActive = false
            },
            cancellationToken);
    }

    private static string ResolveTerminalStatus(CliCommandExecutionResult result)
    {
        return result.State switch
        {
            CliCommandExecutionState.Completed => TerminalStreamStatus.Completed.ToWireValue(),
            CliCommandExecutionState.StillRunning => TerminalStreamStatus.Running.ToWireValue(),
            _ => TerminalStreamStatus.Failed.ToWireValue()
        };
    }

    private static string ResolveCliSessionState(CliCommandExecutionResult result)
    {
        return result.State switch
        {
            CliCommandExecutionState.Completed => CliSessionState.Completed.ToWireValue(),
            CliCommandExecutionState.StillRunning => CliSessionState.Running.ToWireValue(),
            _ => CliSessionState.Failed.ToWireValue()
        };
    }

    private static string ResolveTerminationReason(CliCommandExecutionResult result)
    {
        return result.State switch
        {
            CliCommandExecutionState.Completed => CliSessionTerminationReasons.Completed,
            CliCommandExecutionState.StillRunning => CliSessionTerminationReasons.None,
            CliCommandExecutionState.Cancelled => CliSessionTerminationReasons.Cancelled,
            _ => CliSessionTerminationReasons.ProcessExited
        };
    }

}
