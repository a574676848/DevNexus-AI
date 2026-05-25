using DevNexus.Shared.DTOs;

namespace DevNexus.Infrastructure.Services;

/// <summary>
/// Elasticsearch 禁用态搜索服务。
/// </summary>
public sealed class DisabledElasticsearchSearchService : IElasticsearchSearchService
{
    public Task<SessionSearchResultDto> SearchSessionsAsync(
        string userId,
        string query,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SessionSearchResultDto());
    }

    public Task<List<string>> SearchSessionsByMessageContentAsync(
        string userId,
        string query,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<string>());
    }

    public Task IndexSessionAsync(SessionSearchDocumentDto session, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task BulkIndexSessionsAsync(IEnumerable<SessionSearchDocumentDto> sessions, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task IndexMessageAsync(MessageSearchDocumentDto message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task BulkIndexMessagesAsync(IEnumerable<MessageSearchDocumentDto> messages, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task DeleteSessionMessagesAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task DeleteMessageAsync(string messageId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task DeleteMessagesAsync(IEnumerable<string> messageIds, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task EnsureIndicesExistAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
