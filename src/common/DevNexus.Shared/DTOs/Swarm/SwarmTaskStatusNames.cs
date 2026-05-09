namespace DevNexus.Shared.DTOs.Swarm;

/// <summary>
/// Swarm 任务状态常量定义。
/// </summary>
public static class SwarmTaskStatusNames
{
    public const string Pending = "Pending";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Transferred = "Transferred";
    public const string Skipped = "Skipped";
    public const string GroupChatting = "GroupChatting";
    public const string Evaluating = "Evaluating";
    public const string Retrying = "Retrying";

    /// <summary>
    /// 获取状态对应的中文显示名称。
    /// </summary>
    public static string GetChineseName(string status) => status switch
    {
        Pending => "等待中",
        InProgress => "执行中",
        Completed => "已完成",
        Failed => "失败",
        Transferred => "已流转",
        Skipped => "已跳过",
        GroupChatting => "讨论中",
        Evaluating => "评估中",
        Retrying => "重试中",
        _ => status
    };

    /// <summary>
    /// 获取状态对应的简洁中文显示。
    /// </summary>
    public static string GetShortChineseName(string status) => status switch
    {
        Pending => "等待",
        InProgress => "执行",
        Completed => "完成",
        Failed => "失败",
        Transferred => "流转",
        Skipped => "跳过",
        GroupChatting => "讨论",
        Evaluating => "评估",
        Retrying => "重试",
        _ => status
    };
}
