using System.Runtime.InteropServices;

namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 提供当前客户端运行环境信息。
/// </summary>
public interface IClientEnvironmentService
{
    /// <summary>
    /// 更新平台标识。
    /// </summary>
    string UpdatePlatform { get; }

    /// <summary>
    /// 更新架构标识。
    /// </summary>
    string Architecture { get; }

    /// <summary>
    /// 面向用户的环境名称。
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// 操作系统版本描述。
    /// </summary>
    string OsVersion { get; }
}

/// <summary>
/// 运行环境辅助方法。
/// </summary>
public static class ClientEnvironmentHelper
{
    /// <summary>
    /// 将运行时架构标准化为更新系统使用的字符串。
    /// </summary>
    public static string NormalizeArchitecture(Architecture architecture)
    {
        return architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => architecture.ToString().ToLowerInvariant()
        };
    }
}
