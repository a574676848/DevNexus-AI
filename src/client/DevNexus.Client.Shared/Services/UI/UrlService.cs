using DevNexus.Client.Shared.Abstractions;

namespace DevNexus.Client.Shared.Services.UI;

/// <summary>
/// URL 处理服务实现
/// </summary>
public class UrlService : IUrlService
{
    private readonly AppSettings _settings;

    public UrlService(AppSettings settings)
    {
        _settings = settings;
    }

    /// <inheritdoc />
    public string? GetFullUrl(string? relativeOrAbsoluteUrl)
    {
        if (string.IsNullOrEmpty(relativeOrAbsoluteUrl))
        {
            return null;
        }

        // 如果已经是完整 URL，直接返回
        if (relativeOrAbsoluteUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            relativeOrAbsoluteUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return relativeOrAbsoluteUrl;
        }

        // 如果是相对路径，拼接后端 API 基础 URL
        var baseUrl = _settings.ApiBaseUrl.TrimEnd('/');
        // 确保相对路径以 / 开头
        var path = relativeOrAbsoluteUrl.StartsWith('/') ? relativeOrAbsoluteUrl : $"/{relativeOrAbsoluteUrl}";
        return $"{baseUrl}{path}";
    }
}

