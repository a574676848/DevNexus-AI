using System.Text.Json.Serialization;

namespace DevNexus.Shared.Enums;

/// <summary>
/// CLI 会话模式。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CliSessionMode
{
    /// <summary>
    /// 未知模式。
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 持久化交互式 Shell。
    /// </summary>
    InteractiveShell = 1,

    /// <summary>
    /// 单次命令执行。
    /// </summary>
    OneShotCommand = 2
}
