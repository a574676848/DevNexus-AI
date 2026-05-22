using System.Collections.Concurrent;
using System.Text.Json;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using DevNexus.Shared.Constants;
using DevNexus.Client.Shared.Abstractions;
using DevNexus.Client.Shared.Helpers;
using DevNexus.Client.Shared.Models;
using Microsoft.Extensions.Logging;

namespace DevNexus.Client.Shared.Services.State;

/// <summary>
/// 聊天状态管理实现 - 支持多会话并行
/// </summary>
public partial class ChatState : IChatState
{
    /// <summary>
    /// 多会话状态字典 - 按 SessionId 存储独立状态
    /// </summary>
    private readonly ConcurrentDictionary<Guid, SessionChatState> _sessions = new();
    private readonly ConcurrentDictionary<Guid, CliSessionStateDto> _cliExecSessions = new();
    private readonly ConcurrentDictionary<Guid, ChatSessionRuntimeDto> _sessionRuntimes = new();
    private readonly ILogger<ChatState> _logger;

    /// <summary>
    /// 当前会话 ID
    /// </summary>
    private Guid _currentSessionId;

    /// <summary>
    /// Sidekick 是否可见
    /// </summary>
    private bool _isSidekickVisible;
    private bool _isRealtimeConnected = true;

    private SidekickPaneKind _currentSidekickPane = SidekickPaneKind.None;
    private bool _isTerminalModalVisible;

    public ChatState(ILogger<ChatState> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public event Action? OnStateChanged;

    /// <inheritdoc />
    public Guid CurrentSessionId => _currentSessionId;

    /// <inheritdoc />
    public IReadOnlyList<BlockDto> CurrentBlocks =>
        GetOrCreateSessionState(_currentSessionId).Blocks.AsReadOnly();

    /// <inheritdoc />
    public IReadOnlyList<QueuedChatMessageDto> CurrentQueuedMessages =>
        _currentSessionId != Guid.Empty
            ? GetOrCreateSessionState(_currentSessionId).QueuedMessages.AsReadOnly()
            : Array.Empty<QueuedChatMessageDto>();

    /// <inheritdoc />
    public ArtifactDto? CurrentArtifact =>
        _currentSessionId != Guid.Empty ? GetOrCreateSessionState(_currentSessionId).CurrentArtifact : null;

    /// <inheritdoc />
    public CliSessionStateDto? CurrentCliExecSession =>
        _currentSessionId != Guid.Empty ? GetCliExecSession(_currentSessionId) : null;

    /// <inheritdoc />
    public bool IsSidekickVisible => _isSidekickVisible;

    /// <inheritdoc />
    public SidekickPaneKind CurrentSidekickPane => _currentSidekickPane;

    /// <inheritdoc />
    public TerminalRecordState? CurrentFocusedTerminalRecord =>
        _currentSessionId != Guid.Empty ? GetFocusedTerminalRecord(_currentSessionId) : null;

    /// <inheritdoc />
    public IReadOnlyList<TerminalRecordState> CurrentTerminalRecords =>
        _currentSessionId != Guid.Empty ? GetTerminalRecords(_currentSessionId) : Array.Empty<TerminalRecordState>();

    /// <inheritdoc />
    public IReadOnlyList<PendingInteractionDto> CurrentPendingInteractions =>
        _currentSessionId != Guid.Empty ? GetPendingInteractions(_currentSessionId) : Array.Empty<PendingInteractionDto>();

    /// <inheritdoc />
    public AgentTurnEventsUpdatedDto? CurrentAgentTurnEvents =>
        _currentSessionId != Guid.Empty ? GetAgentTurnEvents(_currentSessionId) : null;

    /// <inheritdoc />
    public bool IsTerminalModalVisible => _isTerminalModalVisible;

    /// <inheritdoc />
    public string CurrentSessionRunStatusText =>
        _currentSessionId != Guid.Empty ? GetSessionRunStatusText(_currentSessionId) : "等待输入";

    #region 多会话支持方法

    private ChatSessionRunState ResolveSessionRunState(Guid sessionId)
    {
        if (_sessionRuntimes.TryGetValue(sessionId, out var runtime))
        {
            var runtimeState = runtime.RunState;
            if (!_isRealtimeConnected && runtimeState == ChatSessionRunState.Generating)
            {
                return ChatSessionRunState.Recovering;
            }

            return runtimeState;
        }

        if (!_sessions.TryGetValue(sessionId, out var state))
        {
            return ChatSessionRunState.Idle;
        }

        if (!_isRealtimeConnected && state.IsGeneratingOptimistic)
        {
            return ChatSessionRunState.Recovering;
        }

        if (state.IsGeneratingOptimistic)
        {
            return ChatSessionRunState.Generating;
        }

        return ChatSessionRunState.Idle;
    }

    /// <inheritdoc />
    public string GetSessionRunStatusText(Guid sessionId)
    {
        if (_sessionRuntimes.TryGetValue(sessionId, out var runtime))
        {
            var runtimeState = runtime.RunState;
            if (!_isRealtimeConnected && runtimeState == ChatSessionRunState.Generating)
            {
                return "连接恢复中，正在等待继续生成";
            }
        }

        var state = ResolveSessionRunState(sessionId);
        return ChatSessionRunStateDisplay.GetDescription(state);
    }

    /// <inheritdoc />
    public ChatSessionRuntimeDto? GetSessionRuntime(Guid sessionId)
    {
        return _sessionRuntimes.TryGetValue(sessionId, out var runtime) ? runtime : null;
    }

    /// <inheritdoc />
    public bool IsSwarmActive(Guid sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var state) && state.IsSwarmActive;
    }

    /// <inheritdoc />
    public CliSessionStateDto? GetCliExecSession(Guid sessionId)
    {
        return _cliExecSessions.TryGetValue(sessionId, out var state) ? state : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<PendingInteractionDto> GetPendingInteractions(Guid sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var state)
            ? state.PendingInteractions.AsReadOnly()
            : Array.Empty<PendingInteractionDto>();
    }

    /// <inheritdoc />
    public AgentTurnEventsUpdatedDto? GetAgentTurnEvents(Guid sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var state) ? state.AgentTurnEvents : null;
    }

    /// <inheritdoc />
    public void UpdateCliExecSession(CliSessionStateDto state)
    {
        _cliExecSessions[state.SessionId] = state;
        ApplyCliExecSessionToTerminalRecord(state);
        InvalidateSessionRuntime(state.SessionId);
        NotifyStateChanged();
    }

    /// <inheritdoc />
    public void ClearCliExecSession(Guid sessionId)
    {
        if (_cliExecSessions.TryRemove(sessionId, out _))
        {
            InvalidateSessionRuntime(sessionId);
            NotifyStateChanged();
        }
    }

    /// <inheritdoc />
    public void SetPendingInteractions(Guid sessionId, IReadOnlyList<PendingInteractionDto> interactions)
    {
        var state = GetOrCreateSessionState(sessionId);
        state.PendingInteractions.Clear();
        state.PendingInteractions.AddRange(interactions);
        InvalidateSessionRuntime(sessionId);
        NotifyStateChanged();
    }

    /// <inheritdoc />
    public void ClearPendingInteractions(Guid sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var state) || state.PendingInteractions.Count == 0)
        {
            return;
        }

        state.PendingInteractions.Clear();
        InvalidateSessionRuntime(sessionId);
        NotifyStateChanged();
    }

    /// <inheritdoc />
    public void AddBlock(Guid sessionId, BlockDto block)
    {
        var state = GetOrCreateSessionState(sessionId);
        if (block.BlockType == BlockType.ToolResult && TryReplaceToolResultBlock(state, block))
        {
            return;
        }

        state.AddBlock(block);
        if (block.BlockType == BlockType.Terminal)
        {
            UpsertTerminalRecord(sessionId, block);
        }
    }

    /// <inheritdoc />
    public void SetSessionGeneratingOptimistic(Guid sessionId, bool generating)
    {
        var state = GetOrCreateSessionState(sessionId);
        if (state.IsGeneratingOptimistic != generating)
        {
            state.SetGeneratingOptimistic(generating);
            InvalidateSessionRuntime(sessionId);
            NotifyStateChanged();
        }
    }

    /// <inheritdoc />
    public void SetRealtimeConnectionState(bool connected)
    {
        if (_isRealtimeConnected == connected)
        {
            return;
        }

        _isRealtimeConnected = connected;
        NotifyStateChanged();
    }

    /// <inheritdoc />
    public void SetQueuedMessages(Guid sessionId, IReadOnlyList<QueuedChatMessageDto> messages)
    {
        var state = GetOrCreateSessionState(sessionId);
        state.QueuedMessages.Clear();
        if (messages.Count > 0)
        {
            state.QueuedMessages.AddRange(messages.OrderBy(message => message.SequenceNumber));
        }
        InvalidateSessionRuntime(sessionId);
        NotifyStateChanged();
    }

    /// <inheritdoc />
    public void RemoveQueuedMessage(Guid sessionId, Guid queuedMessageId)
    {
        var state = GetOrCreateSessionState(sessionId);
        state.QueuedMessages.RemoveAll(message => message.Id == queuedMessageId);
        InvalidateSessionRuntime(sessionId);
        NotifyStateChanged();
    }

    /// <inheritdoc />
    public void SetAgentTurnEvents(Guid sessionId, AgentTurnEventsUpdatedDto eventsUpdate)
    {
        ArgumentNullException.ThrowIfNull(eventsUpdate);

        var state = GetOrCreateSessionState(sessionId);
        eventsUpdate.Events = eventsUpdate.Events
            .OrderBy(item => item.Sequence)
            .ToList();
        state.AgentTurnEvents = eventsUpdate;
        state.LastActiveAt = DateTime.UtcNow;
        NotifyStateChanged();
    }

    /// <inheritdoc />
    public void ClearAgentTurnEvents(Guid sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var state) || state.AgentTurnEvents == null)
        {
            return;
        }

        state.AgentTurnEvents = null;
        state.LastActiveAt = DateTime.UtcNow;
        NotifyStateChanged();
    }

    /// <inheritdoc />
    public void SetSessionRuntime(Guid sessionId, ChatSessionRuntimeDto runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        runtime.SessionId = sessionId;
        _sessionRuntimes[sessionId] = runtime;

        // 统一运行态已明确当前没有挂起交互时，立即清理本地残留，避免旧缓存短暂回显。
        if (runtime.PendingInteractionCount <= 0
            && _sessions.TryGetValue(sessionId, out var state)
            && state.PendingInteractions.Count > 0)
        {
            state.PendingInteractions.Clear();
        }

        NotifyStateChanged();
    }

    /// <inheritdoc />
    public void ClearSessionRuntime(Guid sessionId)
    {
        if (_sessionRuntimes.TryRemove(sessionId, out _))
        {
            NotifyStateChanged();
        }
    }

    /// <inheritdoc />
    public void SetSwarmActive(Guid sessionId, bool active)
    {
        var state = GetOrCreateSessionState(sessionId);
        if (state.IsSwarmActive != active)
        {
            state.IsSwarmActive = active;
            if (sessionId == _currentSessionId)
            {
                if (active)
                {
                    _isSidekickVisible = true;
                    if (_currentSidekickPane is SidekickPaneKind.None or SidekickPaneKind.Swarm)
                    {
                        _currentSidekickPane = SidekickPaneKind.Swarm;
                    }
                }
                else if (_currentSidekickPane == SidekickPaneKind.Swarm)
                {
                    _currentSidekickPane = ResolvePreferredSidekickPane(state);
                    _isSidekickVisible = _currentSidekickPane != SidekickPaneKind.None;
                }
            }
            NotifyStateChanged();
        }
    }
    /// <inheritdoc />
    public void SetBlocks(Guid sessionId, IEnumerable<BlockDto> blocks)
    {
        var state = GetOrCreateSessionState(sessionId);
        var blocksList = blocks?.ToList() ?? new List<BlockDto>();
        state.SetBlocks(blocksList);
        foreach (var block in blocksList.Where(block => block.BlockType == BlockType.Terminal))
        {
            UpsertTerminalRecord(sessionId, block);
        }
        NotifyStateChanged();
    }

    /// <inheritdoc />
    public void SetArtifact(Guid sessionId, ArtifactDto artifact)
    {
        var state = GetOrCreateSessionState(sessionId);
        state.SetArtifact(artifact);

        // 如果是当前会话，自动展开 Sidekick
        if (sessionId == _currentSessionId)
        {
            if (!_isSidekickVisible)
            {
                _isSidekickVisible = true;
                _currentSidekickPane = SidekickPaneKind.Artifact;
            }
            else if (_currentSidekickPane is SidekickPaneKind.None or SidekickPaneKind.Artifact)
            {
                _currentSidekickPane = SidekickPaneKind.Artifact;
            }
        }
        NotifyStateChanged();
    }

    /// <inheritdoc />
    public void Clear(Guid sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var state))
        {
            state.ClearBlocks();
            state.IsSwarmActive = false; // Explicitly reset Swarm state
            NotifyStateChanged();
        }

        _cliExecSessions.TryRemove(sessionId, out _);
        _sessionRuntimes.TryRemove(sessionId, out _);
        ClearTerminalRecords(sessionId);
    }

    #endregion

    #region 当前会话快捷方法

    /// <inheritdoc />
    public void AddBlock(BlockDto block)
    {
        AddBlock(_currentSessionId, block);
    }

    /// <inheritdoc />
    public void SetCurrentSession(Guid sessionId)
    {
        if (_currentSessionId != sessionId)
        {
            _currentSessionId = sessionId;
            // ★ 关键变更：不再清空 Blocks，保留多会话状态
            // 确保该会话的状态存在
            var state = GetOrCreateSessionState(sessionId);

            // ★ 修复：切换会话时，如果目标会话已有 Artifact，自动打开分屏
            // 这确保进入已有代码块的会话时，代码块能正确折叠
            _currentSidekickPane = ResolvePreferredSidekickPane(state);
            _isSidekickVisible = _currentSidekickPane != SidekickPaneKind.None;

            NotifyStateChanged();
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        Clear(_currentSessionId);
    }

    /// <inheritdoc />
    public void SetBlocks(IEnumerable<BlockDto> blocks)
    {
        SetBlocks(_currentSessionId, blocks);
    }

    /// <inheritdoc />
    public void SetArtifact(ArtifactDto artifact)
    {
        SetArtifact(_currentSessionId, artifact);
    }

    /// <inheritdoc />
    public void ToggleSidekick(bool visible)
    {
        if (_isSidekickVisible != visible)
        {
            _isSidekickVisible = visible;
            if (!visible)
            {
                _isTerminalModalVisible = false;
            }
            else if (_currentSessionId != Guid.Empty && _currentSidekickPane == SidekickPaneKind.None)
            {
                _currentSidekickPane = ResolvePreferredSidekickPane(GetOrCreateSessionState(_currentSessionId));
            }
            NotifyStateChanged();
        }
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 获取或创建会话状态
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <returns>会话状态</returns>
    private SessionChatState GetOrCreateSessionState(Guid sessionId)
    {
        return _sessions.GetOrAdd(sessionId, id => new SessionChatState(id));
    }

    private void InvalidateSessionRuntime(Guid sessionId)
    {
        _sessionRuntimes.TryRemove(sessionId, out _);
    }

    /// <summary>
    /// 触发状态变更通知
    /// </summary>
    private void NotifyStateChanged() => OnStateChanged?.Invoke();

    private bool TryReplaceToolResultBlock(SessionChatState state, BlockDto block)
    {
        var toolCallId = GetGuidFromMetadata(block.Metadata, TerminalBlockMetadataKeys.ToolCallId);
        if (!toolCallId.HasValue) return false;

        var existingIndex = state.Blocks.FindIndex(existing =>
        {
            var existingId = GetGuidFromMetadata(existing.Metadata, TerminalBlockMetadataKeys.ToolCallId);
            return existingId.HasValue && existingId.Value == toolCallId.Value;
        });

        if (existingIndex < 0) return false;

        state.Blocks[existingIndex] = block;
        state.LastActiveAt = DateTime.UtcNow;
        return true;
    }

    private static Guid? GetGuidFromMetadata(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value) || value == null)
            return null;

        if (value is JsonElement element && element.ValueKind == JsonValueKind.String)
        {
            if (Guid.TryParse(element.GetString(), out var guid)) return guid;
        }
        else if (Guid.TryParse(value.ToString(), out var guid))
        {
            return guid;
        }

        return null;
    }

    #endregion

    #region 状态清理（可选，防止内存泄漏）

    /// <summary>
    /// 移除指定会话的状态（用于会话删除时清理）
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    public void RemoveSession(Guid sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
        _cliExecSessions.TryRemove(sessionId, out _);
        _sessionRuntimes.TryRemove(sessionId, out _);
        ClearTerminalRecords(sessionId);
    }

    /// <summary>
    /// 清理长时间未活跃的会话状态（可定期调用）
    /// </summary>
    /// <param name="olderThan">超过此时间未活跃的会话将被清理</param>
    public void CleanupInactiveSessions(TimeSpan olderThan)
    {
        var threshold = DateTime.UtcNow - olderThan;
        var inactiveIds = _sessions.Values
            .Where(s => s.LastActiveAt < threshold && s.SessionId != _currentSessionId)
            .Select(s => s.SessionId)
            .ToList();

        foreach (var id in inactiveIds)
        {
            _sessions.TryRemove(id, out _);
            _cliExecSessions.TryRemove(id, out _);
            _sessionRuntimes.TryRemove(id, out _);
            _terminalRecords.TryRemove(id, out _);
        }
    }

    /// <summary>
    /// 清空所有会话状态（用于用户登出时彻底清理）
    /// </summary>
    public void ClearAll()
    {
        _sessions.Clear();
        _cliExecSessions.Clear();
        _sessionRuntimes.Clear();
        _terminalRecords.Clear();
        _currentSessionId = Guid.Empty;
        _isSidekickVisible = false;
        _currentSidekickPane = SidekickPaneKind.None;
        _isTerminalModalVisible = false;
        NotifyStateChanged();
    }

    #endregion
}
