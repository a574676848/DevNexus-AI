namespace DevNexus.Domain.Enums;

/// <summary>
/// 聊天执行决策枚举，用于决定消息的流向
/// </summary>
public enum ChatExecutionDecision
{
    /// <summary>
    /// 立即发送（普通生成或空闲态）
    /// </summary>
    Immediate = 0,

    /// <summary>
    /// 入队等待（长作业占用态）
    /// </summary>
    Queued = 1,

    /// <summary>
    /// 转发给当前运行时作为输入（等待输入态）
    /// </summary>
    ForwardToRuntimeInput = 2,

    /// <summary>
    /// 拒绝（不满足任何发送条件）
    /// </summary>
    Rejected = 3
}
