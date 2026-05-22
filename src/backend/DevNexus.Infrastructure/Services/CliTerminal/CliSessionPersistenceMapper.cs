using DevNexus.Core.Models.Cli;
using DevNexus.Shared.Enums;

namespace DevNexus.Infrastructure.Services.CliTerminal;

/// <summary>
/// CLI 运行态持久化映射工具。
/// </summary>
internal static class CliSessionPersistenceMapper
{
    /// <summary>
    /// 映射共享层 CLI 执行状态。
    /// </summary>
    public static CliExecStatus ToExecStatus(CliSessionExecutionState state)
    {
        return state switch
        {
            CliSessionExecutionState.Created => CliExecStatus.Requested,
            CliSessionExecutionState.Running => CliExecStatus.Running,
            CliSessionExecutionState.WaitingForInput => CliExecStatus.WaitingForInput,
            CliSessionExecutionState.Completed => CliExecStatus.Completed,
            CliSessionExecutionState.Cancelled => CliExecStatus.Cancelled,
            CliSessionExecutionState.TimedOut => CliExecStatus.TimedOut,
            CliSessionExecutionState.Reaped => CliExecStatus.Reaped,
            CliSessionExecutionState.Failed => CliExecStatus.Failed,
            _ => CliExecStatus.Unknown
        };
    }

    /// <summary>
    /// 从 CLI 会话键中解析用户和聊天会话标识。
    /// </summary>
    public static (Guid? UserId, Guid? ChatSessionId) ParseSessionKey(string sessionKey)
    {
        var parts = sessionKey.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return (null, null);
        }

        Guid? userId = null;
        Guid? chatSessionId = null;

        if (parts[0].Length == 32)
        {
            var hex = parts[0];
            if (Guid.TryParseExact(
                $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..32]}",
                "D",
                out var parsedUserId))
            {
                userId = parsedUserId;
            }
        }

        if (Guid.TryParse(parts[1], out var parsedChatSessionId))
        {
            chatSessionId = parsedChatSessionId;
        }

        return (userId, chatSessionId);
    }
}
