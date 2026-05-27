namespace DevNexus.Core.Models.Execution;

/// <summary>
/// CLI 策略配置选项。
/// </summary>
public sealed class CliPolicyOptions
{
    /// <summary>
    /// 是否启用 safe bins 限制。
    /// </summary>
    public bool EnforceSafeBins { get; set; } = true;

    /// <summary>
    /// 默认允许直接执行的命令根。
    /// </summary>
    public string[] SafeBins { get; set; } = [];

    /// <summary>
    /// 永久允许的命令模式或命令根。
    /// 支持完整命令前缀、工作目录命令模式和命令根三种匹配。
    /// </summary>
    public string[] PermanentAllowedCommandPatterns { get; set; } = [];

    /// <summary>
    /// 完全放权模式下是否仍保护高风险命令。
    /// </summary>
    public bool AlwaysProtectHighRisk { get; set; }
}
