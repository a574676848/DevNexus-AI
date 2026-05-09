using DevNexus.Shared.Enums;

namespace DevNexus.Shared.Constants;

/// <summary>
/// 聊天终端相关 DTO 与快照的默认协议值。
/// </summary>
public static class ChatTerminalProtocolDefaults
{
    /// <summary>
    /// 默认终端状态：已完成。
    /// </summary>
    public static readonly string StatusCompleted = TerminalStreamStatus.Completed.ToWireValue();

    /// <summary>
    /// 默认会话状态：已完成。
    /// </summary>
    public static readonly string SessionStateCompleted = CliSessionState.Completed.ToWireValue();

    /// <summary>
    /// 获取终端状态默认值。
    /// </summary>
    public static string GetCompletedStatus()
    {
        return StatusCompleted;
    }

    /// <summary>
    /// 获取会话状态默认值。
    /// </summary>
    public static string GetCompletedSessionState()
    {
        return SessionStateCompleted;
    }
}