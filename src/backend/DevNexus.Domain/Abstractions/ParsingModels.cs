namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 解析上下文
/// </summary>
public class ParsingContext
{
    public string TraceId { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public string? UserId { get; set; } // Added for Kernel Memory tagging
    public CancellationToken CancellationToken { get; set; } = default;
}

/// <summary>
/// 解析选项
/// </summary>
public class ParsingOptions
{
    /// <summary>
    /// 是否启用 Vision 解析 (PDF/Image)
    /// </summary>
    public bool EnableVisionParsing { get; set; } = true;

    /// <summary>
    /// 是否启用 LLM 代码解析 (罕见语言)
    /// </summary>
    public bool EnableLLMCodeParsing { get; set; } = false;

    /// <summary>
    /// 单文档最大成本限制 (美元)
    /// </summary>
    public decimal MaxCostPerDocument { get; set; } = 0.10m;

    /// <summary>
    /// Vision 解析最大页数
    /// </summary>
    public int MaxVisionPages { get; set; } = 20;

    /// <summary>
    /// 最低质量评分阈值 (低于此值使用 Vision)
    /// </summary>
    public double MinimumQualityScore { get; set; } = 0.8;

    /// <summary>
    /// 当前会话 ID (用于上下文关联)
    /// </summary>
    public Guid? SessionId { get; set; }

    /// <summary>
    /// 指定使用的 Provider ID (可选)
    /// </summary>
    public Guid? ProviderId { get; set; }

    /// <summary>
    /// 客户端声明的 MIME 类型（用于服务端一致性校验）
    /// </summary>
    public string? DeclaredMimeType { get; set; }
}
