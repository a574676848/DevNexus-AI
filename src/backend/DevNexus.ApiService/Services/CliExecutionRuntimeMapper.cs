using DevNexus.Core.Models.Cli;
using DevNexus.Core.Services.Cli;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.ApiService.Services;

/// <summary>
/// CLI 运行时快照与事件映射辅助。
/// </summary>
internal static class CliExecutionRuntimeMapper
{
    /// <summary>
    /// 从运行时快照构建 DTO。
    /// </summary>
    public static CliSessionStateDto CreateDto(Guid sessionId, CliSessionRuntimeSnapshot snapshot)
    {
        return CliRuntimeDtoMapper.ToSessionState(sessionId, snapshot);
    }

    /// <summary>
    /// 从持久化实体构建 DTO。
    /// </summary>
    public static CliSessionStateDto CreateDto(CliExecSession session)
    {
        return CliRuntimeDtoMapper.ToSessionState(session);
    }

    /// <summary>
    /// 从元数据构建 DTO。
    /// </summary>
    public static CliSessionStateDto? CreateDto(Guid sessionId, Dictionary<string, object>? metadata)
    {
        if (metadata == null)
        {
            return null;
        }

        var sessionKey = GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.SessionKey);
        var sessionState = CliSessionStateExtensions.Parse(GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.SessionState));
        var status = TerminalStreamStatusExtensions.Parse(GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.Status));
        if (string.IsNullOrWhiteSpace(sessionKey) && !metadata.ContainsKey(TerminalBlockMetadataKeys.WaitingForInput))
        {
            return null;
        }

        return new CliSessionStateDto
        {
            SessionId = sessionId,
            ExecStatus = ToExecStatus(sessionState),
            SessionMode = CliSessionMode.InteractiveShell,
            SessionKey = sessionKey ?? string.Empty,
            TerminalStreamId = GetGuidFromMetadata(metadata, TerminalBlockMetadataKeys.TerminalStreamId),
            Command = GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.Command) ?? string.Empty,
            WorkingDirectory = GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.WorkingDirectory),
            Status = status == TerminalStreamStatus.Unknown ? TerminalStreamStatus.Running.ToWireValue() : status.ToWireValue(),
            SessionState = sessionState == CliSessionState.Unknown ? CliSessionState.Created.ToWireValue() : sessionState.ToWireValue(),
            RuntimeHost = GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.RuntimeHost),
            WaitingForInput = GetBoolFromMetadata(metadata, TerminalBlockMetadataKeys.WaitingForInput) ?? false,
            WaitingForInputSince = GetDateTimeFromMetadata(metadata, TerminalBlockMetadataKeys.WaitingForInputSince),
            StartedAt = GetDateTimeFromMetadata(metadata, TerminalBlockMetadataKeys.StartedAt),
            LastActivityAt = GetDateTimeFromMetadata(metadata, TerminalBlockMetadataKeys.LastActivityAt),
            TerminationReason = CliSessionTerminationReasons.Normalize(
                GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.TerminationReason),
                string.Empty),
            IsActive = sessionState.IsActive()
        };
    }

    /// <summary>
    /// 根据 DTO 解析运行时事件类型。
    /// </summary>
    public static ServerEventType ResolveEventType(CliSessionStateDto state, bool preferOutputEvent = false)
    {
        if (preferOutputEvent)
        {
            return ServerEventType.CliExecOutputUpdated;
        }

        return state.ExecStatus switch
        {
            CliExecStatus.WaitingForInput => ServerEventType.CliExecWaitingForInput,
            CliExecStatus.Completed => ServerEventType.CliExecCompleted,
            CliExecStatus.RolledBack => ServerEventType.CliExecRolledBack,
            CliExecStatus.Cancelled => ServerEventType.CliExecCancelled,
            CliExecStatus.TimedOut => ServerEventType.CliExecTimedOut,
            CliExecStatus.Failed or CliExecStatus.Reaped => ServerEventType.CliExecFailed,
            CliExecStatus.Running or CliExecStatus.Requested or CliExecStatus.Queued => ServerEventType.CliExecStarted,
            _ => ServerEventType.CliExecStarted
        };
    }

    private static CliExecStatus ToExecStatus(CliSessionExecutionState state)
    {
        return state switch
        {
            CliSessionExecutionState.Created => CliExecStatus.Queued,
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

    private static CliExecStatus ToExecStatus(CliSessionState state)
    {
        return state switch
        {
            CliSessionState.Created => CliExecStatus.Requested,
            CliSessionState.Queued => CliExecStatus.Queued,
            CliSessionState.Running => CliExecStatus.Running,
            CliSessionState.WaitingForInput => CliExecStatus.WaitingForInput,
            CliSessionState.Completed => CliExecStatus.Completed,
            CliSessionState.Cancelled => CliExecStatus.Cancelled,
            CliSessionState.TimedOut => CliExecStatus.TimedOut,
            CliSessionState.Reaped => CliExecStatus.Reaped,
            CliSessionState.Failed => CliExecStatus.Failed,
            CliSessionState.RolledBack => CliExecStatus.RolledBack,
            _ => CliExecStatus.Unknown
        };
    }

    private static Guid? GetGuidFromMetadata(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is Guid guidValue)
        {
            return guidValue;
        }

        if (value is System.Text.Json.JsonElement element && element.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            return Guid.TryParse(element.GetString(), out var parsedFromJson) ? parsedFromJson : null;
        }

        return Guid.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }

    private static string? GetStringFromMetadata(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is System.Text.Json.JsonElement element)
        {
            return element.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => element.GetString(),
                System.Text.Json.JsonValueKind.Number => element.ToString(),
                System.Text.Json.JsonValueKind.True => bool.TrueString,
                System.Text.Json.JsonValueKind.False => bool.FalseString,
                _ => null
            };
        }

        return value.ToString();
    }

    private static bool? GetBoolFromMetadata(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is bool boolValue)
        {
            return boolValue;
        }

        if (value is System.Text.Json.JsonElement element)
        {
            return element.ValueKind switch
            {
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                System.Text.Json.JsonValueKind.String when bool.TryParse(element.GetString(), out var parsed) => parsed,
                _ => null
            };
        }

        return bool.TryParse(value.ToString(), out var parsedValue) ? parsedValue : null;
    }

    private static DateTime? GetDateTimeFromMetadata(Dictionary<string, object>? metadata, string key)
    {
        var value = GetStringFromMetadata(metadata, key);
        return DateTime.TryParse(value, out var parsed) ? parsed : null;
    }
}
