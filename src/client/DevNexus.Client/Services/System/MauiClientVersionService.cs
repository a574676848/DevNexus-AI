using DevNexus.Client.Shared.Abstractions;
using Microsoft.Maui.ApplicationModel;

namespace DevNexus.Client.Services.System;

/// <summary>
/// MAUI 客户端版本服务。
/// </summary>
public sealed class MauiClientVersionService : IClientVersionService
{
    /// <inheritdoc />
    public string CurrentVersion
    {
        get
        {
            var version = AppInfo.Current.VersionString;
            return string.IsNullOrWhiteSpace(version) ? "1.0.0.0" : version;
        }
    }
}
