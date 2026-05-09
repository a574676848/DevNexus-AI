using DevNexus.Shared.DTOs;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 代码分析服务接口 (抽象层)
/// 负责代码文件的结构化解析 (AST)
/// </summary>
public interface ICodeAnalysisService
{
    /// <summary>
    /// 分析代码并生成分块
    /// </summary>
    /// <param name="code">代码内容</param>
    /// <param name="language">编程语言 (如 csharp, python)</param>
    /// <returns>包含 AST 信息的分析结果</returns>
    Task<CodeAnalysisResult> AnalyzeCodeAsync(string code, string language);
}

/// <summary>
/// 代码分析结果
/// </summary>
public class CodeAnalysisResult
{
    /// <summary>
    /// 核心分块 (通常按类/方法切分)
    /// </summary>
    public List<SmartChunk> Chunks { get; set; } = new();

    /// <summary>
    /// 代码结构摘要 (JSON 格式的 AST 简报)
    /// </summary>
    public string StructureJson { get; set; } = string.Empty;

    /// <summary>
    /// 代码复杂度/行数等统计
    /// </summary>
    public Dictionary<string, object> Metrics { get; set; } = new();
}
