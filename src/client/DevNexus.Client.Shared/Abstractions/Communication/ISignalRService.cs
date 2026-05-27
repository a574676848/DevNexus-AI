using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.SignalR.Client;

namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// SignalR 实时通信服务接口
/// </summary>
public interface ISignalRService : IAsyncDisposable
{
    /// <summary>
    /// Hub 连接实例（用于扩展订阅）
    /// </summary>
    HubConnection? HubConnection { get; }

    /// <summary>
    /// 接收到 Block 事件
    /// </summary>
    event Action<BlockDto>? OnBlockReceived;

    /// <summary>
    /// 接收到完整消息（增量更新）
    /// </summary>
    event Action<ChatMessageDto>? OnMessageReceived;

    /// <summary>
    /// 接收到 Artifact 状态更新
    /// </summary>
    event Action<ArtifactStatusDto>? OnArtifactStatusReceived;

    /// <summary>
    /// 接收到会话列表更新
    /// </summary>
    event Action<List<ChatSessionDto>>? OnChatSessionsReceived;

    /// <summary>
    /// 连接状态变更事件
    /// </summary>
    event Action<bool>? OnConnectionChanged;

    /// <summary>
    /// 排队消息列表已接收（用于初始化恢复）
    /// </summary>
    event Action<List<QueuedChatMessageDto>>? OnQueuedMessagesReceived;

    /// <summary>
    /// 接收到结构化运行时事件。
    /// </summary>
    event Action<ServerEvent>? OnServerEvent;

    /// <summary>
    /// 是否已连接
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 聊天 Hub 是否已连接。
    /// 该状态用于聊天消息收发、生成控制与 CLI 交互能力判定。
    /// </summary>
    bool IsChatConnected { get; }

    /// <summary>
    /// Artifact Hub 是否已连接。
    /// 该状态用于 Artifact 进度与产物回传能力判定。
    /// </summary>
    bool IsArtifactConnected { get; }

    /// <summary>
    /// 连接到 Hub
    /// </summary>
    Task ConnectAsync();

    /// <summary>
    /// 断开连接
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// 发送聊天消息
    /// </summary>
    Task SendMessageAsync(ChatRequest request);

    /// <summary>
    /// 恢复已解决的挂起交互。
    /// </summary>
    Task ResumePendingInteractionAsync(ChatRequest request);

    /// <summary>
    /// 取消生成
    /// </summary>
    Task CancelGenerationAsync(Guid sessionId);

    /// <summary>
    /// 向 CLI 会话写入一行输入。
    /// </summary>
    Task SendCliInputAsync(Guid sessionId, string input);

    /// <summary>
    /// 终止当前聊天会话关联的 CLI 执行。
    /// </summary>
    Task<CliExecTerminateResultDto?> TerminateCliSessionAsync(Guid sessionId);

    /// <summary>
    /// 回滚当前聊天会话最近一次高风险 CLI 快照。
    /// </summary>
    Task<CliExecRollbackResultDto?> RollbackCliExecSessionAsync(Guid sessionId);

    /// <summary>
    /// 获取指定聊天会话当前可恢复的 CLI 执行会话快照。
    /// </summary>
    Task<CliSessionStateDto?> GetCliExecSessionAsync(Guid sessionId);

    /// <summary>
    /// 轮询指定聊天会话当前的 CLI 执行结果。
    /// </summary>
    Task<CliExecPollResultDto?> PollCliExecSessionAsync(Guid sessionId);

    /// <summary>
    /// 获取指定聊天会话的 CLI 执行日志。
    /// </summary>
    Task<CliExecLogResultDto?> GetCliExecLogAsync(Guid sessionId, int startIndex = 0);

    /// <summary>
    /// 等待指定聊天会话的 CLI 执行进入终态。
    /// </summary>
    Task<CliExecPollResultDto?> WaitCliExecSessionAsync(Guid sessionId, int timeoutMs = 10000);

    /// <summary>
    /// 获取指定会话的排队消息列表。
    /// </summary>
    Task GetQueuedMessagesAsync(Guid sessionId);

    /// <summary>
    /// 取消一条排队消息。
    /// </summary>
    Task CancelQueuedMessageAsync(Guid sessionId, Guid queuedMessageId);

    /// <summary>
    /// 清空指定会话的排队消息。
    /// </summary>
    Task ClearQueuedMessagesAsync(Guid sessionId);

}
