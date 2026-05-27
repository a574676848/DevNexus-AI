using DevNexus.Shared.DTOs;
using DevNexus.Client.Shared.Models;

namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 聊天状态管理接口 - 支持多会话并行
/// </summary>
public interface IChatState
{
    /// <summary>
    /// 状态变更事件
    /// </summary>
    event Action? OnStateChanged;

    /// <summary>
    /// 当前会话 ID
    /// </summary>
    Guid CurrentSessionId { get; }

    /// <summary>
    /// 当前消息块列表 (只读) - 当前会话的 Blocks
    /// </summary>
    IReadOnlyList<BlockDto> CurrentBlocks { get; }

    /// <summary>
    /// 当前显示的资产 (Sidekick) - 当前会话的资产
    /// </summary>
    ArtifactDto? CurrentArtifact { get; }

    /// <summary>
    /// 当前会话的 CLI 执行会话状态。
    /// </summary>
    CliSessionStateDto? CurrentCliExecSession { get; }

    /// <summary>
    /// Sidekick 是否可见
    /// </summary>
    bool IsSidekickVisible { get; }

    /// <summary>
    /// Sidekick 当前显示的主视图类型。
    /// </summary>
    SidekickPaneKind CurrentSidekickPane { get; }

    /// <summary>
    /// 当前会话聚焦的终端记录。
    /// </summary>
    TerminalRecordState? CurrentFocusedTerminalRecord { get; }

    /// <summary>
    /// 当前会话的终端记录列表。
    /// </summary>
    IReadOnlyList<TerminalRecordState> CurrentTerminalRecords { get; }

    /// <summary>
    /// 当前会话的挂起交互列表。
    /// </summary>
    IReadOnlyList<PendingInteractionDto> CurrentPendingInteractions { get; }

    /// <summary>
    /// 当前会话最近一轮工具执行事件批次。
    /// </summary>
    AgentTurnEventsUpdatedDto? CurrentAgentTurnEvents { get; }

    /// <summary>
    /// 当前终端查看弹窗是否可见。
    /// </summary>
    bool IsTerminalModalVisible { get; }

    /// <summary>
    /// 当前会话统一运行状态文案。
    /// </summary>
    string CurrentSessionRunStatusText { get; }

    #region 多会话支持方法

    /// <summary>
    /// 获取指定会话的统一运行状态文案。
    /// </summary>
    string GetSessionRunStatusText(Guid sessionId);

    /// <summary>
    /// 设置指定会话的统一运行时快照。
    /// </summary>
    void SetSessionRuntime(Guid sessionId, ChatSessionRuntimeDto runtime);

    /// <summary>
    /// 清理指定会话的统一运行时快照。
    /// </summary>
    void ClearSessionRuntime(Guid sessionId);

    /// <summary>
    /// 查询指定会话是否处于 Swarm 协同执行状态
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <returns>是否处于 Swarm 协同执行状态</returns>
    bool IsSwarmActive(Guid sessionId);

    /// <summary>
    /// 设置指定会话的 Swarm 协同执行状态
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="active">是否激活</param>
    void SetSwarmActive(Guid sessionId, bool active);

    /// <summary>
    /// 获取指定会话的 CLI 执行会话状态。
    /// </summary>
    CliSessionStateDto? GetCliExecSession(Guid sessionId);

    /// <summary>
    /// 更新指定会话的 CLI 执行会话状态。
    /// </summary>
    void UpdateCliExecSession(CliSessionStateDto state);

    /// <summary>
    /// 清理指定会话的 CLI 执行会话状态。
    /// </summary>
    void ClearCliExecSession(Guid sessionId);

    /// <summary>
    /// 获取指定会话的终端记录列表。
    /// </summary>
    IReadOnlyList<TerminalRecordState> GetTerminalRecords(Guid sessionId);

    /// <summary>
    /// 获取指定会话的挂起交互列表。
    /// </summary>
    IReadOnlyList<PendingInteractionDto> GetPendingInteractions(Guid sessionId);

    /// <summary>
    /// 获取指定会话最近一轮工具执行事件批次。
    /// </summary>
    AgentTurnEventsUpdatedDto? GetAgentTurnEvents(Guid sessionId);

    /// <summary>
    /// 获取指定会话当前聚焦的终端记录。
    /// </summary>
    TerminalRecordState? GetFocusedTerminalRecord(Guid sessionId);

    /// <summary>
    /// 获取指定会话当前统一终端展示状态。
    /// </summary>
    TerminalPresentationState? GetTerminalPresentation(Guid sessionId);

    /// <summary>
    /// 获取指定会话当前工具活动展示状态。
    /// </summary>
    ToolActivityPresentationState? GetToolActivityPresentation(Guid sessionId);

    /// <summary>
    /// 获取指定会话当前消息展示状态。
    /// </summary>
    SessionMessagePresentationState GetSessionMessagePresentation(Guid sessionId);

    /// <summary>
    /// 获取指定会话当前运行态展示状态。
    /// </summary>
    SessionRunPresentationState GetSessionRunPresentation(Guid sessionId);

    /// <summary>
    /// 获取指定会话当前运行态控制语义。
    /// </summary>
    SessionRunControlState GetSessionRunControl(Guid sessionId);

    /// <summary>
    /// 获取指定会话当前统一运行时快照。
    /// </summary>
    ChatSessionRuntimeDto? GetSessionRuntime(Guid sessionId);

    /// <summary>
    /// 同步指定会话当前活跃终端的 CLI 执行日志尾部。
    /// </summary>
    void SyncCliExecLog(Guid sessionId, string output, bool wasTrimmed = false);

    /// <summary>
    /// 追加指定会话当前活跃终端的 CLI 执行日志增量。
    /// </summary>
    void AppendCliExecLog(Guid sessionId, string outputDelta);

    /// <summary>
    /// 用终端块更新指定会话的终端记录。
    /// </summary>
    void UpsertTerminalRecord(Guid sessionId, BlockDto block, bool isFromHistory = false);

    /// <summary>
    /// 使用终端记录 DTO 同步当前会话的活跃终端详情。
    /// </summary>
    void SyncActiveTerminalRecords(Guid sessionId, IReadOnlyList<TerminalRecordDto> records);

    /// <summary>
    /// 批量同步指定会话的历史终端记录。
    /// </summary>
    void MergeTerminalHistory(Guid sessionId, IEnumerable<ChatMessageDto> messages);

    /// <summary>
    /// 清理指定会话的终端记录。
    /// </summary>
    void ClearTerminalRecords(Guid sessionId);

    /// <summary>
    /// 设置指定会话的挂起交互列表。
    /// </summary>
    void SetPendingInteractions(Guid sessionId, IReadOnlyList<PendingInteractionDto> interactions);

    /// <summary>
    /// 清理指定会话的挂起交互。
    /// </summary>
    void ClearPendingInteractions(Guid sessionId);

    /// <summary>
    /// 设置指定会话最近一轮工具执行事件批次。
    /// </summary>
    void SetAgentTurnEvents(Guid sessionId, AgentTurnEventsUpdatedDto eventsUpdate);

    /// <summary>
    /// 清理指定会话最近一轮工具执行事件批次。
    /// </summary>
    void ClearAgentTurnEvents(Guid sessionId);

    /// <summary>
    /// 设置指定会话当前工具活动展示状态。
    /// </summary>
    void SetToolActivity(Guid sessionId, ToolActivityPresentationState activity);

    /// <summary>
    /// 清理指定会话当前工具活动展示状态。
    /// </summary>
    void ClearToolActivity(Guid sessionId);

    /// <summary>
    /// 聚焦指定会话的终端记录。
    /// </summary>
    void FocusTerminalRecord(Guid sessionId, Guid? recordId, bool openSidekick = true);

    /// <summary>
    /// 根据终端块聚焦指定会话的终端记录。
    /// </summary>
    void FocusTerminalRecord(Guid sessionId, BlockDto block, bool openSidekick = true);

    /// <summary>
    /// 打开聊天终端 Sidekick。
    /// </summary>
    void OpenChatTerminalSidekick(Guid sessionId, Guid? recordId = null);

    /// <summary>
    /// 打开 Swarm Sidekick。
    /// </summary>
    void OpenSwarmSidekick(Guid sessionId);

    /// <summary>
    /// 打开 Artifact Sidekick。
    /// </summary>
    void OpenArtifactSidekick(Guid sessionId);

    /// <summary>
    /// 打开终端查看弹窗。
    /// </summary>
    void OpenTerminalModal();

    /// <summary>
    /// 关闭终端查看弹窗。
    /// </summary>
    void CloseTerminalModal();

    /// <summary>
    /// 按会话 ID 添加消息块
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="block">消息块</param>
    void AddBlock(Guid sessionId, BlockDto block);

    /// <summary>
    /// 设置指定会话的短时 optimistic 生成态。
    /// 仅用于本地立即反馈，正式运行态以后端 runtime 快照为准。
    /// </summary>
    void SetSessionGeneratingOptimistic(Guid sessionId, bool generating);

    /// <summary>
    /// 设置实时链路连接状态。
    /// </summary>
    void SetRealtimeConnectionState(bool connected);

    /// <summary>
    /// 按会话 ID 设置消息块列表
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="blocks">消息块列表</param>
    void SetBlocks(Guid sessionId, IEnumerable<BlockDto> blocks);

    /// <summary>
    /// 按会话 ID 设置资产
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="artifact">资产信息</param>
    void SetArtifact(Guid sessionId, ArtifactDto artifact);

    /// <summary>
    /// 按会话 ID 清空消息块
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    void Clear(Guid sessionId);

    #endregion

    #region 当前会话快捷方法

    /// <summary>
    /// 添加消息块到当前会话
    /// </summary>
    void AddBlock(BlockDto block);

    /// <summary>
    /// 设置当前会话
    /// </summary>
    void SetCurrentSession(Guid sessionId);

    /// <summary>
    /// 清空当前会话消息
    /// </summary>
    void Clear();

    /// <summary>
    /// 批量设置当前会话消息块
    /// </summary>
    void SetBlocks(IEnumerable<BlockDto> blocks);

    /// <summary>
    /// 设置当前资产并自动展开 Sidekick
    /// </summary>
    void SetArtifact(ArtifactDto artifact);

    /// <summary>
    /// 切换 Sidekick 可见性
    /// </summary>
    void ToggleSidekick(bool visible);

    /// <summary>
    /// 清空所有会话状态（用于用户登出时彻底清理）
    /// </summary>
    void ClearAll();

    #endregion

    #region 排队消息状态

    /// <summary>
    /// 当前会话的排队消息列表。
    /// </summary>
    IReadOnlyList<QueuedChatMessageDto> CurrentQueuedMessages { get; }

    /// <summary>
    /// 设置指定会话的排队消息列表。
    /// </summary>
    void SetQueuedMessages(Guid sessionId, IReadOnlyList<QueuedChatMessageDto> messages);

    /// <summary>
    /// 移除指定会话的一条排队消息。
    /// </summary>
    void RemoveQueuedMessage(Guid sessionId, Guid queuedMessageId);

    #endregion
}
