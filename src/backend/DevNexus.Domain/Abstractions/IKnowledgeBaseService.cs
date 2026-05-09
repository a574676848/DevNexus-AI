using DevNexus.Shared.DTOs;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 知识库服务接口
/// 提供文档向量化存储和语义搜索功能
/// </summary>
public interface IKnowledgeBaseService
{
    /// <summary>
    /// 将文档及其 Chunks 存入向量数据库（全局模式，兼容旧调用）
    /// </summary>
    Task UpsertDocumentAsync(SmartDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// 将文档及其 Chunks 存入向量数据库（用户级隔离）
    /// </summary>
    /// <param name="document">SmartDocument 文档</param>
    /// <param name="userId">用户ID，用于隔离知识库</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpsertDocumentAsync(SmartDocument document, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 语义搜索（全局模式，兼容旧调用）
    /// </summary>
    Task<List<SmartChunk>> SearchAsync(string query, int limit = 5, double minScore = 0.7);

    /// <summary>
    /// 语义搜索（用户级隔离）
    /// </summary>
    /// <param name="query">查询文本</param>
    /// <param name="userId">用户ID，用于过滤知识库</param>
    /// <param name="limit">返回结果数量上限</param>
    /// <param name="minScore">最小相似度阈值</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>匹配的文档片段列表</returns>
    Task<List<SmartChunk>> SearchAsync(string query, Guid userId, int limit = 5, double minScore = 0.7, CancellationToken cancellationToken = default);
}
