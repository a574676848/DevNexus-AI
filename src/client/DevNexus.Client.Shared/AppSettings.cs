namespace DevNexus.Client.Shared;

/// <summary>
/// 应用程序配置
/// </summary>
public class AppSettings
{
    /// <summary>
    /// API 基础地址
    /// </summary>
    public string ApiBaseUrl { get; set; } = DefaultApiBaseUrl;

    /// <summary>
    /// SignalR Hub 地址
    /// </summary>
    public string SignalRHubUrl { get; set; } = "https://localhost:7321/hubs/chat";

    public const string DefaultApiBaseUrl = "https://localhost:7321";
}
