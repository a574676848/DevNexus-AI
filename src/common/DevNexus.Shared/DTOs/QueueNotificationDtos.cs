namespace DevNexus.Shared.DTOs;

/// <summary>
/// 消息已入队通知数据
/// </summary>
public record QueuedMessageAcceptedData(
    Guid? QueuedMessageId,
    Guid SessionId,
    int PendingCount,
    string? Message);

/// <summary>
/// 排队消息开始派发通知数据
/// </summary>
public record QueuedMessageStartedData(
    Guid QueuedMessageId,
    Guid SessionId);

/// <summary>
/// 排队消息已移除通知数据
/// </summary>
public record QueuedMessageRemovedData(
    Guid QueuedMessageId,
    Guid SessionId);

/// <summary>
/// 排队消息已清空通知数据
/// </summary>
public record QueuedMessagesClearedData(
    Guid SessionId,
    int ClearedCount);
