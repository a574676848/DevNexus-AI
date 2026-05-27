using DevNexus.Core.Models.Evaluation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 聊天执行上下文 - 在异步调用链中传递会话、消息与工具执行信息
/// </summary>
public static class ChatExecutionContext
{
    private static readonly AsyncLocal<ChatExecutionContextState?> _current = new();
    private static readonly AsyncLocal<ImmutableStack<Guid>?> _toolCallStack = new();

    /// <summary>
    /// 当前上下文是否已初始化
    /// </summary>
    public static bool HasActive => _current.Value != null;

    /// <summary>
    /// 当前会话 ID
    /// </summary>
    public static Guid CurrentSessionId => _current.Value?.SessionId ?? Guid.Empty;

    /// <summary>
    /// 当前消息 ID
    /// </summary>
    public static Guid CurrentMessageId => _current.Value?.MessageId ?? Guid.Empty;

    /// <summary>
    /// 当前重试次数（0 表示首次尝试）
    /// </summary>
    public static int CurrentAttemptNumber => _current.Value?.AttemptNumber ?? 0;

    /// <summary>
    /// 当前 Agent 审批模式。
    /// </summary>
    public static AgentApprovalMode CurrentApprovalMode =>
        _current.Value?.ApprovalMode ?? AgentApprovalMode.AskUser;

    /// <summary>
    /// 当前工具调用 ID
    /// </summary>
    public static Guid CurrentToolCallId
    {
        get
        {
            var stack = _toolCallStack.Value;
            // 检查栈是否存在且不为空，避免在空栈上调用 Peek() 导致异常
            if (stack != null && !stack.IsEmpty)
            {
                return stack.Peek();
            }
            return Guid.Empty;
        }
    }

    /// <summary>
    /// 初始化上下文（Attempt 级别）
    /// </summary>
    public static void Begin(
        Guid sessionId,
        Guid messageId,
        int attemptNumber,
        AgentApprovalMode approvalMode = AgentApprovalMode.AskUser)
    {
        _current.Value = new ChatExecutionContextState
        {
            SessionId = sessionId,
            MessageId = messageId,
            AttemptNumber = attemptNumber,
            ApprovalMode = approvalMode,
            ToolRecords = new ConcurrentQueue<ToolExecutionRecord>()
        };

        _toolCallStack.Value = ImmutableStack<Guid>.Empty;
    }

    /// <summary>
    /// 设置工具调用 ID
    /// </summary>
    public static void PushToolCallId(Guid toolCallId)
    {
        var stack = _toolCallStack.Value ?? ImmutableStack<Guid>.Empty;
        _toolCallStack.Value = stack.Push(toolCallId);
    }

    /// <summary>
    /// 清理工具调用 ID
    /// </summary>
    public static void PopToolCallId()
    {
        var stack = _toolCallStack.Value ?? ImmutableStack<Guid>.Empty;
        if (stack.IsEmpty)
        {
            return;
        }

        _toolCallStack.Value = stack.Pop();
    }

    /// <summary>
    /// 添加工具执行记录
    /// </summary>
    public static void AddToolRecord(ToolExecutionRecord record)
    {
        var state = _current.Value;
        if (state == null)
        {
            return;
        }

        state.ToolRecords.Enqueue(record);
    }

    /// <summary>
    /// 获取工具执行记录快照
    /// </summary>
    public static IReadOnlyList<ToolExecutionRecord> GetToolRecordsSnapshot()
    {
        var state = _current.Value;
        if (state == null || state.ToolRecords.IsEmpty)
        {
            return Array.Empty<ToolExecutionRecord>();
        }

        return state.ToolRecords.ToArray();
    }

    /// <summary>
    /// 获取上下文快照
    /// </summary>
    public static ChatExecutionContextSnapshot GetSnapshot()
    {
        var state = _current.Value;
        if (state == null)
        {
            return ChatExecutionContextSnapshot.Empty;
        }

        return new ChatExecutionContextSnapshot(
            state.SessionId,
            state.MessageId,
            state.AttemptNumber,
            CurrentToolCallId,
            state.ApprovalMode);
    }

    /// <summary>
    /// 清理上下文
    /// </summary>
    public static void Clear()
    {
        _current.Value = null;
        _toolCallStack.Value = null;
    }

    private sealed class ChatExecutionContextState
    {
        public Guid SessionId { get; set; }

        public Guid MessageId { get; set; }

        public int AttemptNumber { get; set; }

        public AgentApprovalMode ApprovalMode { get; set; } = AgentApprovalMode.AskUser;

        public ConcurrentQueue<ToolExecutionRecord> ToolRecords { get; set; } = new();
    }
}

/// <summary>
/// 聊天执行上下文快照
/// </summary>
public readonly struct ChatExecutionContextSnapshot
{
    /// <summary>
    /// 空快照
    /// </summary>
    public static readonly ChatExecutionContextSnapshot Empty = new(
        Guid.Empty,
        Guid.Empty,
        0,
        Guid.Empty,
        AgentApprovalMode.AskUser);

    /// <summary>
    /// 会话 ID
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// 消息 ID
    /// </summary>
    public Guid MessageId { get; }

    /// <summary>
    /// 重试次数
    /// </summary>
    public int AttemptNumber { get; }

    /// <summary>
    /// 工具调用 ID
    /// </summary>
    public Guid ToolCallId { get; }

    /// <summary>
    /// Agent 审批模式。
    /// </summary>
    public AgentApprovalMode ApprovalMode { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    public ChatExecutionContextSnapshot(
        Guid sessionId,
        Guid messageId,
        int attemptNumber,
        Guid toolCallId,
        AgentApprovalMode approvalMode)
    {
        SessionId = sessionId;
        MessageId = messageId;
        AttemptNumber = attemptNumber;
        ToolCallId = toolCallId;
        ApprovalMode = approvalMode;
    }
}
