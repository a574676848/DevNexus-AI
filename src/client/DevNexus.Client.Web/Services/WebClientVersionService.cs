using DevNexus.Client.Shared.Abstractions;

namespace DevNexus.Client.Web.Services;

/// <summary>
/// Web 客户端版本服务。
/// </summary>
public sealed class WebClientVersionService : IClientVersionService
{
    /// <inheritdoc />
    public string CurrentVersion
    {
        get
        {
            var version = typeof(WebClientVersionService).Assembly.GetName().Version?.ToString();
            return string.IsNullOrWhiteSpace(version) ? "1.0.0.0" : version;
        }
    }
}
