namespace DevNexus.Shared.DTOs;

/// <summary>
/// 解析响应
/// </summary>
public class ParseDocumentResponse
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 解析后的文档 (如果是同步解析或解析完成)
    /// </summary>
    public SmartDocument? SmartDocument { get; set; }
    
    /// <summary>
    /// 追踪 ID (用于异步轮询)
    /// </summary>
    public string? TraceId { get; set; }
}
