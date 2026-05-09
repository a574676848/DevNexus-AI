using System.Collections.Concurrent;
using DevNexus.Client.Shared.Models;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Client.Shared.Services.State;

public partial class ChatState
{
    private readonly ConcurrentDictionary<Guid, Dictionary<Guid, TerminalRecordState>> _terminalRecords = new();

    public IReadOnlyList<TerminalRecordState> GetTerminalRecords(Guid sessionId)
    {
        if (!_terminalRecords.TryGetValue(sessionId, out var records) || records.Count == 0)
        {
            return Array.Empty<TerminalRecordState>();
        }

        return records.Values
            .OrderByDescending(record => record.IsActive)
            .ThenByDescending(record => record.LastActivityAt ?? record.StartedAt ?? DateTime.MinValue)
            .ThenByDescending(record => record.MessageId)
            .ToList();
    }

    public TerminalRecordState? GetFocusedTerminalRecord(Guid sessionId)
    {
        var sessionState = GetOrCreateSessionState(sessionId);
        if (!_terminalRecords.TryGetValue(sessionId, out var records) || records.Count == 0)
        {
            return null;
        }

        if (sessionState.FocusedTerminalRecordId.HasValue
            && records.TryGetValue(sessionState.FocusedTerminalRecordId.Value, out var focused))
        {
            return focused;
        }

        var fallback = records.Values
            .OrderByDescending(record => record.IsActive)
            .ThenByDescending(record => record.LastActivityAt ?? record.StartedAt ?? DateTime.MinValue)
            .FirstOrDefault();

        sessionState.FocusedTerminalRecordId = fallback?.RecordId;
        return fallback;
    }

    public void SyncCliExecLog(Guid sessionId, string output, bool wasTrimmed = false)
    {
        if (!_terminalRecords.TryGetValue(sessionId, out var records) || records.Count == 0)
        {
            return;
        }

        var target = records.Values
            .Where(record => record.IsActive)
            .OrderByDescending(record => record.LastActivityAt ?? record.StartedAt ?? DateTime.MinValue)
            .FirstOrDefault()
            ?? GetFocusedTerminalRecord(sessionId);

        if (target == null)
        {
            return;
        }

        target.SyncOutput(output, wasTrimmed);
        NotifyStateChanged();
    }

    public void AppendCliExecLog(Guid sessionId, string outputDelta)
    {
        if (string.IsNullOrEmpty(outputDelta)
            || !_terminalRecords.TryGetValue(sessionId, out var records)
            || records.Count == 0)
        {
            return;
        }

        var target = records.Values
            .Where(record => record.IsActive)
            .OrderByDescending(record => record.LastActivityAt ?? record.StartedAt ?? DateTime.MinValue)
            .FirstOrDefault()
            ?? GetFocusedTerminalRecord(sessionId);

        if (target == null)
        {
            return;
        }

        target.AppendOutputDelta(outputDelta);
        NotifyStateChanged();
    }

    public void UpsertTerminalRecord(Guid sessionId, BlockDto block, bool isFromHistory = false)
    {
        if (block.BlockType != BlockType.Terminal)
        {
            return;
        }

        var next = TerminalRecordState.FromBlock(block, isFromHistory);
        if (next == null)
        {
            return;
        }

        var sessionRecords = _terminalRecords.GetOrAdd(sessionId, _ => new Dictionary<Guid, TerminalRecordState>());
        if (sessionRecords.TryGetValue(next.RecordId, out var existing))
        {
            existing.ApplyBlock(block, isFromHistory);
        }
        else
        {
            sessionRecords[next.RecordId] = next;
        }

        var sessionState = GetOrCreateSessionState(sessionId);
        if (!sessionState.FocusedTerminalRecordId.HasValue
            || sessionState.FocusedTerminalRecordId == next.RecordId
            || next.IsActive)
        {
            sessionState.FocusedTerminalRecordId = next.RecordId;
        }

        if (sessionId == _currentSessionId && next.IsActive)
        {
            // 终端摘要卡已经出现时，说明当前会话已经拿到了活跃终端记录。
            // 此时默认同步展开右侧终端分屏，避免用户只能看到摘要卡却无法直接跟进输出。
            if (_currentSidekickPane is SidekickPaneKind.None or SidekickPaneKind.ChatTerminal)
            {
                _isSidekickVisible = true;
                _currentSidekickPane = SidekickPaneKind.ChatTerminal;
            }
        }

        NotifyStateChanged();
    }

    public void SyncActiveTerminalRecords(Guid sessionId, IReadOnlyList<TerminalRecordDto> records)
    {
        var sessionRecords = _terminalRecords.GetOrAdd(sessionId, _ => new Dictionary<Guid, TerminalRecordState>());
        var activeIds = new HashSet<Guid>();

        foreach (var record in records)
        {
            activeIds.Add(record.RecordId);

            if (sessionRecords.TryGetValue(record.RecordId, out var existing))
            {
                existing.SessionId = record.SessionId;
                existing.MessageId = record.MessageId;
                existing.TerminalStreamId = record.TerminalStreamId;
                existing.ToolCallId = record.ToolCallId;
                existing.PackageId = record.PackageId;
                existing.Command = record.Command;
                existing.WorkingDirectory = record.WorkingDirectory;
                existing.Status = record.Status;
                existing.SessionState = record.SessionState;
                existing.RuntimeHost = record.RuntimeHost;
                existing.ExitCode = record.ExitCode;
                existing.AttemptNumber = record.AttemptNumber;
                existing.IsRetry = record.IsRetry;
                existing.WaitingForInput = record.WaitingForInput;
                existing.WaitingForInputSince = record.WaitingForInputSince;
                existing.TerminationReason = record.TerminationReason;
                existing.StartedAt = record.StartedAt;
                existing.LastActivityAt = record.LastActivityAt;
                existing.SyncOutput(record.Output ?? string.Empty);
                existing.IsActive = record.IsActive;
                existing.IsFromHistory = false;
                existing.HasArchivedOutput = record.HasArchivedOutput;
                existing.OutputLength = record.OutputLength;
                existing.OutputLineCount = record.OutputLineCount;
                existing.WatchSummary = record.WatchSummary;
            }
            else
            {
                sessionRecords[record.RecordId] = new TerminalRecordState
                {
                    RecordId = record.RecordId,
                    SessionId = record.SessionId,
                    MessageId = record.MessageId,
                    TerminalStreamId = record.TerminalStreamId,
                    ToolCallId = record.ToolCallId,
                    PackageId = record.PackageId,
                    Command = record.Command,
                    WorkingDirectory = record.WorkingDirectory,
                    Status = record.Status,
                    SessionState = record.SessionState,
                    RuntimeHost = record.RuntimeHost,
                    ExitCode = record.ExitCode,
                    AttemptNumber = record.AttemptNumber,
                    IsRetry = record.IsRetry,
                    WaitingForInput = record.WaitingForInput,
                    WaitingForInputSince = record.WaitingForInputSince,
                    TerminationReason = record.TerminationReason,
                    StartedAt = record.StartedAt,
                    LastActivityAt = record.LastActivityAt,
                    Output = record.Output ?? string.Empty,
                    OutputWasTrimmed = false,
                    IsActive = record.IsActive,
                    IsFromHistory = false,
                    HasArchivedOutput = record.HasArchivedOutput,
                    OutputLength = record.OutputLength,
                    OutputLineCount = record.OutputLineCount,
                    WatchSummary = record.WatchSummary
                };
                sessionRecords[record.RecordId].SyncOutput(record.Output ?? string.Empty);
            }
        }

        var staleActiveIds = sessionRecords.Values
            .Where(item => item.IsActive && !activeIds.Contains(item.RecordId))
            .Select(item => item.RecordId)
            .ToList();

        foreach (var staleId in staleActiveIds)
        {
            if (sessionRecords.TryGetValue(staleId, out var stale))
            {
                stale.IsActive = false;
                stale.WaitingForInput = false;
            }
        }

        EnsureFocusedTerminalRecord(sessionId);
        NotifyStateChanged();
    }

    public void MergeTerminalHistory(Guid sessionId, IEnumerable<ChatMessageDto> messages)
    {
        if (messages == null)
        {
            return;
        }

        foreach (var message in messages)
        {
            if (message.OrderedBlocks == null || message.OrderedBlocks.Count == 0)
            {
                continue;
            }

            foreach (var block in message.OrderedBlocks.Where(block => block.BlockType == BlockType.Terminal))
            {
                UpsertTerminalRecord(sessionId, block, isFromHistory: true);
            }
        }

        EnsureFocusedTerminalRecord(sessionId);
        NotifyStateChanged();
    }

    public void ClearTerminalRecords(Guid sessionId)
    {
        _terminalRecords.TryRemove(sessionId, out _);

        if (sessionId == _currentSessionId)
        {
            var sessionState = GetOrCreateSessionState(sessionId);
            sessionState.FocusedTerminalRecordId = null;

            if (_currentSidekickPane == SidekickPaneKind.ChatTerminal)
            {
                _currentSidekickPane = ResolvePreferredSidekickPane(sessionState);
                _isSidekickVisible = _currentSidekickPane != SidekickPaneKind.None;
            }

            NotifyStateChanged();
        }
    }

    public void FocusTerminalRecord(Guid sessionId, Guid? recordId, bool openSidekick = true)
    {
        var sessionState = GetOrCreateSessionState(sessionId);
        sessionState.FocusedTerminalRecordId = recordId;

        if (openSidekick)
        {
            OpenChatTerminalSidekick(sessionId, recordId);
            return;
        }

        NotifyStateChanged();
    }

    public void FocusTerminalRecord(Guid sessionId, BlockDto block, bool openSidekick = true)
    {
        UpsertTerminalRecord(sessionId, block, isFromHistory: true);
        var record = TerminalRecordState.FromBlock(block, isFromHistory: true);
        FocusTerminalRecord(sessionId, record?.RecordId, openSidekick);
    }

    public void OpenChatTerminalSidekick(Guid sessionId, Guid? recordId = null)
    {
        var sessionState = GetOrCreateSessionState(sessionId);
        if (recordId.HasValue)
        {
            sessionState.FocusedTerminalRecordId = recordId.Value;
        }

        _currentSidekickPane = SidekickPaneKind.ChatTerminal;
        _isSidekickVisible = true;
        NotifyStateChanged();
    }

    public void OpenSwarmSidekick(Guid sessionId)
    {
        if (_currentSessionId == sessionId || _currentSessionId == Guid.Empty)
        {
            _currentSidekickPane = SidekickPaneKind.Swarm;
            _isSidekickVisible = true;
            NotifyStateChanged();
        }
    }

    public void OpenArtifactSidekick(Guid sessionId)
    {
        if (_currentSessionId == sessionId || _currentSessionId == Guid.Empty)
        {
            _currentSidekickPane = SidekickPaneKind.Artifact;
            _isSidekickVisible = true;
            NotifyStateChanged();
        }
    }

    public void OpenTerminalModal()
    {
        if (CurrentFocusedTerminalRecord == null)
        {
            return;
        }

        _isTerminalModalVisible = true;
        NotifyStateChanged();
    }

    public void CloseTerminalModal()
    {
        if (_isTerminalModalVisible)
        {
            _isTerminalModalVisible = false;
            NotifyStateChanged();
        }
    }

    private bool HasTerminalRecords(Guid sessionId)
    {
        return _terminalRecords.TryGetValue(sessionId, out var records) && records.Count > 0;
    }

    private void ApplyCliExecSessionToTerminalRecord(CliSessionStateDto state)
    {
        if (!_terminalRecords.TryGetValue(state.SessionId, out var records) || records.Count == 0)
        {
            return;
        }

        TerminalRecordState? target = null;
        if (state.TerminalStreamId.HasValue)
        {
            target = records.Values.FirstOrDefault(record => record.TerminalStreamId == state.TerminalStreamId);
        }

        target ??= records.Values
            .Where(record => record.IsActive)
            .OrderByDescending(record => record.LastActivityAt ?? record.StartedAt ?? DateTime.MinValue)
            .FirstOrDefault();

        if (target == null)
        {
            return;
        }

        target.SessionId = state.SessionId;
        target.TerminalStreamId = state.TerminalStreamId ?? target.TerminalStreamId;
        target.Command = string.IsNullOrWhiteSpace(state.Command) ? target.Command : state.Command;
        target.WorkingDirectory = string.IsNullOrWhiteSpace(state.WorkingDirectory)
            ? target.WorkingDirectory
            : state.WorkingDirectory;
        target.Status = state.Status;
        target.SessionState = state.SessionState;
        target.RuntimeHost = state.RuntimeHost ?? target.RuntimeHost;
        target.WaitingForInput = state.WaitingForInput;
        target.WaitingForInputSince = state.WaitingForInputSince;
        target.TerminationReason = state.TerminationReason;
        target.IsActive = state.IsActive;
        target.LastActivityAt = state.LastActivityAt;
    }

    private void EnsureFocusedTerminalRecord(Guid sessionId)
    {
        var sessionState = GetOrCreateSessionState(sessionId);
        if (sessionState.FocusedTerminalRecordId.HasValue
            && _terminalRecords.TryGetValue(sessionId, out var records)
            && records.ContainsKey(sessionState.FocusedTerminalRecordId.Value))
        {
            return;
        }

        var fallback = GetTerminalRecords(sessionId).FirstOrDefault();
        sessionState.FocusedTerminalRecordId = fallback?.RecordId;
    }
}
