using System.Runtime.InteropServices;
using DevNexus.Client.Shared.Abstractions;

namespace DevNexus.Client.Web.Services;

/// <summary>
/// Web 客户端运行环境信息。
/// </summary>
public sealed class WebClientEnvironmentService : IClientEnvironmentService
{
    public string UpdatePlatform => "web";

    public string Architecture => "browser";

    public string DisplayName => "Web";

    public string OsVersion => RuntimeInformation.OSDescription;
}
