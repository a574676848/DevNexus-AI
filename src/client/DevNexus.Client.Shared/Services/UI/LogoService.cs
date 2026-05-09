namespace DevNexus.Client.Shared.Services.UI;

/// <summary>
/// Logo 服务实现
/// 在应用启动时扫描 wwwroot/images/providers 目录并缓存所有 Logo 路径
/// </summary>
public class LogoService : ILogoService
{
    private readonly IUrlService _urlService;
    private readonly List<string> _presetLogos = new();
    private readonly HashSet<string> _presetLogoSet = new(StringComparer.OrdinalIgnoreCase);
    private bool _isInitialized;
    private readonly object _lock = new();

    public LogoService(IUrlService urlService)
    {
        _urlService = urlService;
    }

    /// <inheritdoc />
    public Task InitializeAsync()
    {
        if (_isInitialized) return Task.CompletedTask;

        lock (_lock)
        {
            if (_isInitialized) return Task.CompletedTask;

            try
            {
                // 在 MAUI Blazor 中，静态资源位于 wwwroot 目录
                // 由于我们无法直接访问文件系统中的 wwwroot，
                // 我们使用预定义的 Logo 列表，但设计为可扩展
                LoadPresetLogos();
                _isInitialized = true;

            }
            catch (Exception)
            {

            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetPresetLogos()
    {
        if (!_isInitialized)
        {
            _ = InitializeAsync();
        }
        return _presetLogos.AsReadOnly();
    }

    /// <inheritdoc />
    public bool IsPresetLogo(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;

        if (!_isInitialized)
        {
            _ = InitializeAsync();
        }

        return _presetLogoSet.Contains(path) || _presetLogoSet.Contains("/" + path);
    }

    /// <inheritdoc />
    public string GetLogoUrl(string? logoUrl, string defaultLogo = "/images/providers/default.svg")
    {
        if (!_isInitialized)
        {
            _ = InitializeAsync();
        }

        // 如果 logoUrl 为空，返回默认 Logo
        if (string.IsNullOrEmpty(logoUrl))
        {
            return defaultLogo;
        }

        // 如果是预设 Logo（本地静态资源），直接返回相对路径
        if (IsPresetLogo(logoUrl))
        {
            return logoUrl;
        }

        // 如果已经是完整 URL，直接返回
        if (logoUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            logoUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return logoUrl;
        }

        // 其他情况（远程相对路径），使用 UrlService 转换
        return _urlService.GetFullUrl(logoUrl) ?? defaultLogo;
    }

    /// <inheritdoc />
    public string GetDefaultLogo(string? providerId)
    {
        if (string.IsNullOrEmpty(providerId))
            return "/images/providers/default.svg";

        var lowerProviderId = providerId.ToLower();

        return lowerProviderId switch
        {
            // AI 模型供应商
            "openai" or "openai-compatible" => "/images/providers/openai-compatible.svg",
            "google" => "/images/providers/google.svg",
            "gemini" => "/images/providers/gemini.svg",
            "deepseek" => "/images/providers/deepseek.svg",
            "moonshot" or "kimi" => "/images/providers/kimi.svg",
            "qwen" or "dashscope" or "aliyun" or "tongyi" => "/images/providers/qwen.svg",
            "zhipu" or "glm" or "chatglm" => "/images/providers/glm.svg",
            "minimax" => "/images/providers/minimax.svg",
            "hunyuan" or "tencent" => "/images/providers/tencent.svg",
            
            // 搜索供应商
            "searxng" => "/images/providers/searxng.svg",
            "tavily" => "/images/providers/tavily.svg",
            "jinareader" => "/images/providers/jinareader.svg",
            "firecrawl" => "/images/providers/firecrawl.svg",
            
            // 存储供应商
            "aws" or "awss3" or "s3" => "/images/providers/aws.svg",
            "aliyunoss" or "oss" => "/images/providers/aliyun.svg",
            "qiniukodo" or "qiniu" => "/images/providers/qiniu.svg",
            "tencentcos" or "cos" => "/images/providers/tencent.svg",
            "cloudflarer2" or "r2" => "/images/providers/cloudflare.svg",
            
            // 笔记供应商
            "memos" => "/images/providers/memos.svg",
            "notion" => "/images/providers/notion.svg",
            "obsidian" => "/images/providers/obsidian.svg",

            _ => "/images/providers/default.svg"
        };
    }

    /// <summary>
    /// 加载预设 Logo 列表
    /// 按照优先级排序：常用供应商在前
    /// </summary>
    private void LoadPresetLogos()
    {
        // 预设 Logo 列表 - 按照供应商类别和常见程度排序
        var logos = new[]
        {
            // AI 模型供应商
            "/images/providers/openai-compatible.svg",
            "/images/providers/gemini.svg",
            "/images/providers/deepseek.svg",
            "/images/providers/kimi.svg",
            "/images/providers/qwen.svg",
            "/images/providers/glm.svg",
            "/images/providers/minimax.svg",
            
            // 搜索与解析供应商
            "/images/providers/searxng.svg",
            "/images/providers/tavily.svg",
            "/images/providers/jinareader.svg",
            "/images/providers/firecrawl.svg",
            
            // 云存储与基础设施
            "/images/providers/aws.svg",
            "/images/providers/aliyun.svg",
            "/images/providers/tencent.svg",
            "/images/providers/cloudflare.svg",
            "/images/providers/qiniu.svg",
            
            // 笔记系统
            "/images/providers/memos.svg",
            "/images/providers/notion.svg",
            "/images/providers/obsidian.svg",
            
            // 默认图标
            "/images/providers/default.svg"
        };

        foreach (var logo in logos)
        {
            if (!_presetLogoSet.Contains(logo))
            {
                _presetLogos.Add(logo);
                _presetLogoSet.Add(logo);
            }
        }
    }
}

