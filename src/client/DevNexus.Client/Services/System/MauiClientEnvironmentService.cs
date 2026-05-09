using System.Runtime.InteropServices;
using DevNexus.Client.Shared.Abstractions;

namespace DevNexus.Client.Services.System;

/// <summary>
/// 原生客户端运行环境信息。
/// </summary>
public sealed class MauiClientEnvironmentService : IClientEnvironmentService
{
    /// <inheritdoc />
    public string UpdatePlatform
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return "desktop-windows";
            }

            if (OperatingSystem.IsMacCatalyst())
            {
                return "desktop-macos";
            }

            return "desktop";
        }
    }

    /// <inheritdoc />
    public string Architecture => ClientEnvironmentHelper.NormalizeArchitecture(RuntimeInformation.ProcessArchitecture);

    /// <inheritdoc />
    public string DisplayName
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return "Windows Desktop";
            }

            if (OperatingSystem.IsMacCatalyst())
            {
                return "macOS Desktop";
            }

            return "Desktop";
        }
    }

    /// <inheritdoc />
    public string OsVersion => RuntimeInformation.OSDescription;
}
