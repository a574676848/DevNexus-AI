using System.Text.Json;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Client.Shared.Models;

/// <summary>
/// 客户端终端记录状态，用于聊天记录卡片、Sidekick 面板和历史回看。
/// </summary>
public sealed class TerminalRecordState
{
    private const int MaxOutputChars = 120_000;
    private const int TargetOutputChars = 90_000;
    private const string TrimBanner = "[较早输出已截断，当前仅保留最近内容]";

    public Guid RecordId { get; set; }

    public Guid SessionId { get; set; }

    public Guid MessageId { get; set; }

    public Guid? TerminalStreamId { get; set; }

    public Guid? ToolCallId { get; set; }

    public string? PackageId { get; set; }

    public string Command { get; set; } = string.Empty;

    public string? WorkingDirectory { get; set; }

    public string Status { get; set; } = nameof(TerminalStreamStatus.Completed);

    public string SessionState { get; set; } = nameof(CliSessionState.Completed);

    public string? RuntimeHost { get; set; }

    public int? ExitCode { get; set; }

    public int AttemptNumber { get; set; }

    public bool IsRetry { get; set; }

    public bool WaitingForInput { get; set; }

    public DateTime? WaitingForInputSince { get; set; }

    public string? TerminationReason { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? LastActivityAt { get; set; }

    public string Output { get; set; } = string.Empty;

    public bool OutputWasTrimmed { get; set; }

    public bool HasArchivedOutput { get; set; }

    public int OutputLength { get; set; }

    public int OutputLineCount { get; set; }

    public string? WatchSummary { get; set; }

    public bool IsActive { get; set; }

    public bool IsFromHistory { get; set; }

    public static TerminalRecordState? FromBlock(BlockDto block, bool isFromHistory = false)
    {
        if (block.BlockType != DevNexus.Shared.Enums.BlockType.Terminal)
        {
            return null;
        }

        var terminalStreamId = GetGuid(block.Metadata, TerminalBlockMetadataKeys.TerminalStreamId);
        var recordId = terminalStreamId ?? block.BlockId;
        if (recordId == Guid.Empty)
        {
            return null;
        }

        return new TerminalRecordState
        {
            RecordId = recordId,
            SessionId = block.SessionId,
            MessageId = block.MessageId,
            TerminalStreamId = terminalStreamId,
            ToolCallId = GetGuid(block.Metadata, TerminalBlockMetadataKeys.ToolCallId),
            PackageId = GetString(block.Metadata, TerminalBlockMetadataKeys.PackageId),
            Command = GetString(block.Metadata, TerminalBlockMetadataKeys.Command) ?? string.Empty,
            WorkingDirectory = GetString(block.Metadata, TerminalBlockMetadataKeys.WorkingDirectory),
            Status = TerminalStreamStatusExtensions.Parse(GetString(block.Metadata, TerminalBlockMetadataKeys.Status)).ToWireValue(),
            SessionState = CliSessionStateExtensions.Parse(
                    GetString(block.Metadata, TerminalBlockMetadataKeys.SessionState) ?? GetString(block.Metadata, TerminalBlockMetadataKeys.Status))
                .ToWireValue(),
            RuntimeHost = GetString(block.Metadata, TerminalBlockMetadataKeys.RuntimeHost),
            ExitCode = GetInt(block.Metadata, TerminalBlockMetadataKeys.ExitCode),
            AttemptNumber = GetInt(block.Metadata, TerminalBlockMetadataKeys.AttemptNumber) ?? 0,
            IsRetry = GetBool(block.Metadata, TerminalBlockMetadataKeys.IsRetry) ?? false,
            WaitingForInput = GetBool(block.Metadata, TerminalBlockMetadataKeys.WaitingForInput) ?? false,
            WaitingForInputSince = GetDateTime(block.Metadata, TerminalBlockMetadataKeys.WaitingForInputSince),
            TerminationReason = CliSessionTerminationReasons.Normalize(
                GetString(block.Metadata, TerminalBlockMetadataKeys.TerminationReason),
                string.Empty),
            StartedAt = GetDateTime(block.Metadata, TerminalBlockMetadataKeys.StartedAt),
            LastActivityAt = GetDateTime(block.Metadata, TerminalBlockMetadataKeys.LastActivityAt),
            Output = NormalizeOutputWindow(block.Content ?? string.Empty, out var outputWasTrimmed),
            OutputWasTrimmed = outputWasTrimmed,
            HasArchivedOutput = GetBool(block.Metadata, TerminalBlockMetadataKeys.HasArchivedOutput) ?? false,
            OutputLength = GetInt(block.Metadata, TerminalBlockMetadataKeys.OutputLength) ?? (block.Content?.Length ?? 0),
            OutputLineCount = GetInt(block.Metadata, TerminalBlockMetadataKeys.OutputLineCount) ?? CountLines(block.Content ?? string.Empty),
            WatchSummary = GetString(block.Metadata, TerminalBlockMetadataKeys.WatchSummary),
            IsActive = GetBool(block.Metadata, TerminalBlockMetadataKeys.IsActive)
                ?? IsSessionActive(GetString(block.Metadata, TerminalBlockMetadataKeys.SessionState)
                    ?? GetString(block.Metadata, TerminalBlockMetadataKeys.Status)),
            IsFromHistory = isFromHistory
        };
    }

    public void ApplyBlock(BlockDto block, bool isFromHistory = false)
    {
        var next = FromBlock(block, isFromHistory);
        if (next == null)
        {
            return;
        }

        SessionId = next.SessionId;
        MessageId = next.MessageId;
        TerminalStreamId = next.TerminalStreamId;
        ToolCallId = next.ToolCallId;
        PackageId = next.PackageId;
        Command = next.Command;
        WorkingDirectory = next.WorkingDirectory;
        Status = next.Status;
        SessionState = next.SessionState;
        RuntimeHost = next.RuntimeHost;
        ExitCode = next.ExitCode;
        AttemptNumber = next.AttemptNumber;
        IsRetry = next.IsRetry;
        WaitingForInput = next.WaitingForInput;
        WaitingForInputSince = next.WaitingForInputSince;
        TerminationReason = next.TerminationReason;
        StartedAt = next.StartedAt;
        LastActivityAt = next.LastActivityAt;
        IsActive = next.IsActive;
        IsFromHistory = isFromHistory;
        HasArchivedOutput = next.HasArchivedOutput;
        OutputLength = next.OutputLength;
        OutputLineCount = next.OutputLineCount;
        WatchSummary = next.WatchSummary;

        if (!string.IsNullOrEmpty(block.Content))
        {
            if (isFromHistory)
            {
                Output = next.Output;
                OutputWasTrimmed = next.OutputWasTrimmed;
            }
            else
            {
                AppendOutputDelta(block.Content);
            }
        }
        else if (isFromHistory)
        {
            Output = next.Output;
            OutputWasTrimmed = next.OutputWasTrimmed;
        }
    }

    public bool Matches(Guid recordId)
    {
        return RecordId == recordId;
    }

    public bool MatchesStream(Guid? terminalStreamId)
    {
        return terminalStreamId.HasValue && TerminalStreamId == terminalStreamId;
    }

    private static bool IsSessionActive(string? sessionState)
    {
        return CliSessionStateExtensions.Parse(sessionState).IsActive();
    }

    /// <summary>
    /// 仅保留最近一段终端输出，避免单个终端文本无限增长拖垮前端。
    /// </summary>
    public void SyncOutput(string output, bool wasTrimmed = false)
    {
        Output = NormalizeOutputWindow(output, out var normalizedTrimmed);
        OutputWasTrimmed = wasTrimmed || normalizedTrimmed;
    }

    public void AppendOutputDelta(string outputDelta)
    {
        if (string.IsNullOrEmpty(outputDelta))
        {
            return;
        }

        SyncOutput($"{StripTrimBanner(Output)}{outputDelta}", OutputWasTrimmed);
    }

    public static string NormalizeOutputWindow(string output, out bool wasTrimmed)
    {
        wasTrimmed = false;
        if (string.IsNullOrEmpty(output) || output.Length <= MaxOutputChars)
        {
            return output;
        }

        var startIndex = Math.Max(0, output.Length - TargetOutputChars);
        var lineBreakIndex = output.IndexOf('\n', startIndex);
        if (lineBreakIndex >= 0 && lineBreakIndex < output.Length - 1)
        {
            startIndex = lineBreakIndex + 1;
        }

        wasTrimmed = startIndex > 0;
        var suffix = output[startIndex..];
        return $"{TrimBanner}{Environment.NewLine}{suffix}";
    }

    private static int CountLines(string output)
    {
        if (string.IsNullOrEmpty(output))
        {
            return 0;
        }

        return output.Count(character => character == '\n') + 1;
    }

    private static string StripTrimBanner(string output)
    {
        if (string.IsNullOrEmpty(output))
        {
            return string.Empty;
        }

        var prefix = $"{TrimBanner}{Environment.NewLine}";
        return output.StartsWith(prefix, StringComparison.Ordinal)
            ? output[prefix.Length..]
            : output;
    }

    private static string? GetString(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.ToString(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => null
            };
        }

        return value.ToString();
    }

    private static Guid? GetGuid(Dictionary<string, object>? metadata, string key)
    {
        var value = GetString(metadata, key);
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }

    private static int? GetInt(Dictionary<string, object>? metadata, string key)
    {
        var value = GetString(metadata, key);
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static bool? GetBool(Dictionary<string, object>? metadata, string key)
    {
        var value = GetString(metadata, key);
        return bool.TryParse(value, out var parsed) ? parsed : null;
    }

    private static DateTime? GetDateTime(Dictionary<string, object>? metadata, string key)
    {
        var value = GetString(metadata, key);
        return DateTime.TryParse(value, out var parsed) ? parsed : null;
    }
}
