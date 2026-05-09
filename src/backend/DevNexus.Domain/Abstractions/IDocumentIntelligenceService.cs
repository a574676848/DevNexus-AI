using DevNexus.Shared.DTOs;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 文档智能分析服务接口 (抽象层)
/// 负责屏蔽具体的文档解析技术 (如 Local PdfPig, Azure Document Intelligence, Python Sidecar)
/// </summary>
public interface IDocumentIntelligenceService
{
    /// <summary>
    /// 分析文档流，提取结构化内容
    /// </summary>
    /// <param name="fileStream">文件流</param>
    /// <param name="mimeType">MIME 类型</param>
    /// <param name="context">解析上下文</param>
    /// <returns>分析结果 (包含 Chunks)</returns>
    Task<DocumentAnalysisResult> AnalyzeDocumentAsync(
        Stream fileStream, 
        string mimeType,
        ParsingContext? context = null);
}

/// <summary>
/// 文档分析结果
/// </summary>
public class DocumentAnalysisResult
{
    /// <summary>
    /// 提取出的分块列表
    /// </summary>
    public List<SmartChunk> Chunks { get; set; } = new();

    /// <summary>
    /// 完整的文本内容 (用于全文索引或简单的 TextDocumentContent)
    /// </summary>
    public string FullText { get; set; } = string.Empty;

    /// <summary>
    /// 识别到的元数据 (如 Language, PageCount)
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// 质量评分 (0.0 - 1.0)
    /// </summary>
    public double QualityScore { get; set; } = 1.0;
}
