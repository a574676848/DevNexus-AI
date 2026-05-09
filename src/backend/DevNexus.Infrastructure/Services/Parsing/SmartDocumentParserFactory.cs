// using DevNexus.Domain.Abstractions via GlobalUsings
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Parsing;

/// <summary>
/// 智能文档解析器工厂
/// 根据文件类型分发到对应的解析器
/// </summary>
public class SmartDocumentParserFactory : ISmartDocumentParser
{
    private readonly IEnumerable<ISmartDocumentParser> _parsers;
    private readonly ILogger<SmartDocumentParserFactory> _logger;
    private readonly Dictionary<string, ISmartDocumentParser> _parserMap = new(StringComparer.OrdinalIgnoreCase);

    public SmartDocumentParserFactory(
        IEnumerable<ISmartDocumentParser> parsers,
        ILogger<SmartDocumentParserFactory> logger)
    {
        _parsers = parsers;
        _logger = logger;
        
        InitializeParserMap();
    }

    private void InitializeParserMap()
    {
        // 自动发现并构建映射
        // 注意：这里排除了工厂自身，防止无限递归 (如果工厂也注册为 ISmartDocumentParser)
        // 实际上 Factory 通常单独注册，或者在这里过滤
        foreach (var parser in _parsers)
        {
            if (parser is SmartDocumentParserFactory) continue;

            // 这里我们需要一种方式知道 Parser 支持哪些 MIME
            // 简单方案：遍历常见 MIME 询问 CanParse
            // 优化方案：Parser 暴露 SupportedMimeTypes 属性 (需要改接口，风险较大)
            // 折中方案：即时查找 (On-demand lookup) 或 预定义映射
            
            // 由于 CanParse(string) 是运行时检查，这里我们采用 "Lazy Lookup" 策略
            // 不在构造函数里构建完整映射，而是在 ParseAsync 时查找
        }
    }

    public bool CanParse(string mimeType)
    {
        return _parsers.Any(p => p is not SmartDocumentParserFactory && p.CanParse(mimeType));
    }

    public async Task<SmartDocument> ParseAsync(
        Stream fileStream, 
        string fileName, 
        string? mimeType = null, 
        ParsingOptions? options = null,
        ParsingContext? context = null)
    {
        // 确定 MIME 类型
        var effectiveMimeType = mimeType;
        if (string.IsNullOrEmpty(effectiveMimeType) || effectiveMimeType == "application/octet-stream")
        {
            effectiveMimeType = GetMimeType(fileName);
        }

        _logger.LogDebug("Parsing File: {FileName}, MIME: {MimeType}", fileName, effectiveMimeType);

        // 查找合适的 Parser
        // 优先匹配 CanParse 返回 true 的解析器
        var parser = _parsers.FirstOrDefault(p => p is not SmartDocumentParserFactory && p.CanParse(effectiveMimeType));

        if (parser == null)
        {
            _logger.LogWarning("Unsupported MimeType: {MimeType}", effectiveMimeType);
            throw new NotSupportedException($"No parser found for {effectiveMimeType}");
        }

        return await parser.ParseAsync(fileStream, fileName, effectiveMimeType, options, context);
    }

    /// <summary>
    /// 根据文件名获取 MIME 类型
    /// 使用 FileExtensionContentTypeProvider 覆盖 800+ 常见类型
    /// 对编程语言等特殊类型保留自定义映射
    /// </summary>
    private static readonly FileExtensionContentTypeProvider _contentTypeProvider = new();
    
    /// <summary>
    /// 编程语言的自定义 MIME 类型映射（内置 Provider 不包含）
    /// </summary>
    private static readonly Dictionary<string, string> _programmingLanguageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".cs", "text/x-csharp" },
        { ".java", "text/x-java" },
        { ".ts", "text/typescript" },
        { ".tsx", "text/typescript" },
        { ".py", "text/x-python" },
        { ".go", "text/x-go" },
        { ".rs", "text/x-rust" },
        { ".rb", "text/x-ruby" },
        { ".php", "text/x-php" },
        { ".swift", "text/x-swift" },
        { ".kt", "text/x-kotlin" },
        { ".scala", "text/x-scala" },
        { ".vue", "text/x-vue" },
        { ".jsx", "text/javascript" },
        { ".sql", "text/x-sql" },
        { ".sh", "text/x-shellscript" },
        { ".bat", "text/x-batch" },
        { ".ps1", "text/x-powershell" },
        { ".yaml", "text/yaml" },
        { ".yml", "text/yaml" },
        { ".toml", "text/x-toml" },
        { ".dockerfile", "text/x-dockerfile" },
        { ".proto", "text/x-protobuf" },
        { ".graphql", "text/x-graphql" },
        { ".md", "text/markdown" }
    };

    public static string GetMimeType(string fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant() ?? "";
        
        // 1. 优先检查编程语言类型（内置 Provider 不包含）
        if (_programmingLanguageMimeTypes.TryGetValue(extension, out var langMimeType))
        {
            return langMimeType;
        }
        
        // 2. 使用内置 Provider（覆盖 800+ 常见类型）
        if (_contentTypeProvider.TryGetContentType(fileName, out var contentType))
        {
            return contentType;
        }
        
        // 3. 兜底
        return "application/octet-stream";
    }
}
