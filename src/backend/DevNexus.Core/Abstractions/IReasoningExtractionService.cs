namespace DevNexus.Core.Abstractions;

/// <summary>
/// 推理内容提取服务接口
/// 用于从不同 LLM 提供商的响应中提取思维链/推理过程
/// </summary>
public interface IReasoningExtractionService
{
    /// <summary>
    /// 尝试提取推理内容
    /// </summary>
    /// <param name="metadata">元数据字典</param>
    /// <param name="innerContent">原始响应对象 (SDK 特定类型)</param>
    /// <returns>提取到的推理内容，如果无法提取则返回 null</returns>
    string? Extract(IReadOnlyDictionary<string, object?>? metadata, object? innerContent);
}
