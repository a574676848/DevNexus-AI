using DevNexus.Shared.DTOs;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// Elasticsearch 搜索服务接口（Infrastructure 层内部接口）
/// 这是 ISearchService 的具体实现接口，定义 ES 特有的功能
/// </summary>
public interface IElasticsearchSearchService
{
    /// <summary>
    /// 搜索会话
    /// </summary>
    Task<SessionSearchResultDto> SearchSessionsAsync(
        string userId,
        string query,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 通过消息内容搜索会话
    /// </summary>
    Task<List<string>> SearchSessionsByMessageContentAsync(
        string userId,
        string query,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 索引会话
    /// </summary>
    Task IndexSessionAsync(SessionSearchDocumentDto session, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量索引会话
    /// </summary>
    Task BulkIndexSessionsAsync(IEnumerable<SessionSearchDocumentDto> sessions, CancellationToken cancellationToken = default);

    /// <summary>
    /// 索引消息
    /// </summary>
    Task IndexMessageAsync(MessageSearchDocumentDto message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量索引消息
    /// </summary>
    Task BulkIndexMessagesAsync(IEnumerable<MessageSearchDocumentDto> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除会话索引
    /// </summary>
    Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除会话下的所有消息索引
    /// </summary>
    Task DeleteSessionMessagesAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除单个消息索引
    /// </summary>
    Task DeleteMessageAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量删除消息索引
    /// </summary>
    Task DeleteMessagesAsync(IEnumerable<string> messageIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查搜索服务是否可用
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 确保索引存在
    /// </summary>
    Task EnsureIndicesExistAsync(CancellationToken cancellationToken = default);
}
