namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// URL 处理服务接口
/// </summary>
public interface IUrlService
{
    /// <summary>
    /// 将相对路径转换为完整 URL
    /// </summary>
    /// <param name="relativeOrAbsoluteUrl">相对路径或完整 URL</param>
    /// <returns>完整 URL</returns>
    string? GetFullUrl(string? relativeOrAbsoluteUrl);
}

