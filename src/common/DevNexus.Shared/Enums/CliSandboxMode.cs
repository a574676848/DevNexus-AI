using System.Text.Json.Serialization;

namespace DevNexus.Shared.Enums;

/// <summary>
/// CLI sandbox 模式。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CliSandboxMode
{
    /// <summary>
    /// 宿主直连模式。
    /// 不进入容器 sandbox，直接在当前宿主机受控 shell 中执行。
    /// </summary>
    LocalRestricted = 1,

    /// <summary>
    /// 容器隔离模式。
    /// </summary>
    ContainerIsolated = 2
}
