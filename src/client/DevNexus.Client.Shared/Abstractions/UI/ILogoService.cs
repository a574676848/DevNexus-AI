namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// Logo 服务接口
/// 提供供应商 Logo 的缓存和访问功能
/// </summary>
public interface ILogoService
{
    /// <summary>
    /// 初始化服务，扫描并缓存所有本地 Logo
    /// 应在应用启动时调用一次
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// 获取所有预设 Logo 的路径列表
    /// </summary>
    /// <returns>预设 Logo 路径的只读列表</returns>
    IReadOnlyList<string> GetPresetLogos();

    /// <summary>
    /// 检查指定路径是否为预设 Logo
    /// </summary>
    /// <param name="path">Logo 路径</param>
    /// <returns>是否为预设 Logo</returns>
    bool IsPresetLogo(string? path);

    /// <summary>
    /// 获取 Logo 的完整 URL（统一处理逻辑）
    /// - 预设 Logo：直接返回相对路径（本地静态资源）
    /// - 远程 Logo：拼接 API 基础 URL
    /// - 空值：返回默认 Logo 路径
    /// </summary>
    /// <param name="logoUrl">Logo URL（可能是相对路径或完整 URL）</param>
    /// <param name="defaultLogo">默认 Logo 路径（当 logoUrl 为空时使用）</param>
    /// <returns>可用于 img src 的完整 URL</returns>
    string GetLogoUrl(string? logoUrl, string defaultLogo = "/images/providers/default.svg");

    /// <summary>
    /// 根据供应商标识符（如 "openai"）获取对应的默认 Logo 路径
    /// </summary>
    /// <param name="providerId">供应商标识符</param>
    /// <returns>Logo 路径</returns>
    string GetDefaultLogo(string? providerId);
}

