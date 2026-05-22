using System.Text.Json.Serialization;
using DevNexus.Shared.Enums;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 会话统一运行时快照 DTO。
/// </summary>
public class ChatSessionRuntimeDto
{
    /// <summary>
    /// 会话标识。
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 统一运行态。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ChatSessionRunState RunState { get; set; } = ChatSessionRunState.Idle;

    /// <summary>
    /// 当前活跃挂起交互数量。
    /// </summary>
    public int PendingInteractionCount { get; set; }

    /// <summary>
    /// 主挂起交互类型。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PendingInteractionKind PrimaryPendingInteractionKind { get; set; } = PendingInteractionKind.Unknown;

    /// <summary>
    /// 主挂起交互标识。
    /// </summary>
    public Guid? PrimaryPendingInteractionId { get; set; }

    /// <summary>
    /// 主挂起交互标题。
    /// </summary>
    public string? PrimaryPendingInteractionTitle { get; set; }

    /// <summary>
    /// 主挂起交互说明。
    /// </summary>
    public string? PrimaryPendingInteractionDescription { get; set; }

    /// <summary>
    /// 主挂起交互摘要。
    /// </summary>
    public PendingInteractionSummaryDto? PrimaryPendingInteractionSummary { get; set; }

    /// <summary>
    /// 当前等待中的排队消息数量。
    /// </summary>
    public int QueuedCount { get; set; }

    /// <summary>
    /// 是否存在活跃 CLI 会话。
    /// </summary>
    public bool HasActiveCliSession { get; set; }

    /// <summary>
    /// 当前 CLI 是否等待输入。
    /// </summary>
    public bool CliWaitingForInput { get; set; }

    /// <summary>
    /// 当前是否存在进行中的助手消息。
    /// </summary>
    public bool HasInProgressAssistantMessage { get; set; }
}
