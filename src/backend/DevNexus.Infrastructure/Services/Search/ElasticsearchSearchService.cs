using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DevNexus.Shared.DTOs;
// using DevNexus.Domain.Configuration via GlobalUsings
using DevNexus.Infrastructure.Models.Elasticsearch;
// using DevNexus.Domain.Abstractions via GlobalUsings

namespace DevNexus.Infrastructure.Services;

/// <summary>
/// Elasticsearch 搜索服务实现
/// 使用 Elastic.Clients.Elasticsearch 9.2.2 版本 API
/// </summary>
public class ElasticsearchSearchService : IElasticsearchSearchService
{
    private readonly ElasticsearchClient _client;
    private readonly ElasticsearchOptions _options;
    private readonly ILogger<ElasticsearchSearchService> _logger;
    private bool _indicesChecked;

    public ElasticsearchSearchService(
        IOptions<ElasticsearchOptions> options,
        ILogger<ElasticsearchSearchService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var settings = new ElasticsearchClientSettings(new Uri(_options.Url));

        // 配置认证
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            settings = settings.Authentication(new ApiKey(_options.ApiKey));
        }
        else if (!string.IsNullOrEmpty(_options.Username) && !string.IsNullOrEmpty(_options.Password))
        {
            settings = settings.Authentication(new BasicAuthentication(_options.Username, _options.Password));
        }

        // 配置默认索引
        settings = settings
            .DefaultIndex(_options.SessionIndexName)
            .EnableDebugMode()
            .RequestTimeout(TimeSpan.FromSeconds(30));

        _client = new ElasticsearchClient(settings);

        _logger.LogDebug("Elasticsearch 客户端已初始化，服务地址: {Url}", _options.Url);
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.PingAsync(cancellationToken);
            return response.IsValidResponse;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Elasticsearch 服务不可用");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task EnsureIndicesExistAsync(CancellationToken cancellationToken = default)
    {
        if (_indicesChecked) return;

        try
        {
            // 检查会话索引是否存在
            var sessionIndexExists = await _client.Indices.ExistsAsync(_options.SessionIndexName, cancellationToken);
            if (!sessionIndexExists.Exists)
            {
                var createSessionIndex = await _client.Indices.CreateAsync(_options.SessionIndexName, c => c
                    .Mappings(m => m
                        .Properties<SessionDocument>(p => p
                            .Keyword(k => k.Id)
                            .Keyword(k => k.UserId)
                            .Text(t => t.Title, td => td
                                .Analyzer("ik_max_word")
                                .SearchAnalyzer("ik_smart"))
                            .Date(d => d.CreatedAt)
                            .Date(d => d.UpdatedAt)
                            .Text(t => t.LastMessagePreview, td => td
                                .Analyzer("ik_max_word")
                                .SearchAnalyzer("ik_smart"))
                            .IntegerNumber(i => i.MessageCount)
                        )
                    ), cancellationToken);

                if (!createSessionIndex.IsValidResponse)
                {
                    _logger.LogWarning("创建会话索引失败: {Error}", createSessionIndex.DebugInformation);
                }
                else
                {
                    _logger.LogDebug("会话索引 {IndexName} 创建成功", _options.SessionIndexName);
                }
            }

            // 检查消息索引是否存在
            var messageIndexExists = await _client.Indices.ExistsAsync(_options.MessageIndexName, cancellationToken);
            if (!messageIndexExists.Exists)
            {
                var createMessageIndex = await _client.Indices.CreateAsync(_options.MessageIndexName, c => c
                    .Mappings(m => m
                        .Properties<MessageDocument>(p => p
                            .Keyword(k => k.Id)
                            .Keyword(k => k.SessionId)
                            .Keyword(k => k.UserId)
                            .Keyword(k => k.Role)
                            .Text(t => t.Content, td => td
                                .Analyzer("ik_max_word")
                                .SearchAnalyzer("ik_smart"))
                            .Date(d => d.CreatedAt)
                        )
                    ), cancellationToken);

                if (!createMessageIndex.IsValidResponse)
                {
                    _logger.LogWarning("创建消息索引失败: {Error}", createMessageIndex.DebugInformation);
                }
                else
                {
                    _logger.LogDebug("消息索引 {IndexName} 创建成功", _options.MessageIndexName);
                }
            }

            _indicesChecked = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "确保索引存在时发生错误");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task IndexSessionAsync(SessionSearchDocumentDto session, CancellationToken cancellationToken = default)
    {
        await EnsureIndicesExistAsync(cancellationToken);

        var doc = MapToDocument(session);
        var response = await _client.IndexAsync(doc, i => i
            .Index(_options.SessionIndexName)
            .Id(doc.Id), cancellationToken);

        if (!response.IsValidResponse)
        {
            _logger.LogWarning("索引会话失败: {SessionId}, Error: {Error}", session.Id, response.DebugInformation);
        }
        else
        {
            _logger.LogDebug("会话已索引: {SessionId}", session.Id);
        }
    }

    /// <inheritdoc />
    public async Task BulkIndexSessionsAsync(IEnumerable<SessionSearchDocumentDto> sessions, CancellationToken cancellationToken = default)
    {
        await EnsureIndicesExistAsync(cancellationToken);

        var docs = sessions.Select(MapToDocument);
        var response = await _client.BulkAsync(b => b
            .Index(_options.SessionIndexName)
            .IndexMany(docs), cancellationToken);

        if (!response.IsValidResponse)
        {
            _logger.LogWarning("批量索引会话失败: {Error}", response.DebugInformation);
        }
        else
        {
            _logger.LogDebug("批量索引会话完成，数量: {Count}", sessions.Count());
        }
    }

    /// <inheritdoc />
    public async Task IndexMessageAsync(MessageSearchDocumentDto message, CancellationToken cancellationToken = default)
    {
        await EnsureIndicesExistAsync(cancellationToken);

        var doc = MapToDocument(message);
        var response = await _client.IndexAsync(doc, i => i
            .Index(_options.MessageIndexName)
            .Id(doc.Id), cancellationToken);

        if (!response.IsValidResponse)
        {
            _logger.LogWarning("索引消息失败: {MessageId}, Error: {Error}", message.Id, response.DebugInformation);
        }
        else
        {
            _logger.LogDebug("消息已索引: {MessageId}", message.Id);
        }
    }

    /// <inheritdoc />
    public async Task BulkIndexMessagesAsync(IEnumerable<MessageSearchDocumentDto> messages, CancellationToken cancellationToken = default)
    {
        await EnsureIndicesExistAsync(cancellationToken);

        var docs = messages.Select(MapToDocument);
        var response = await _client.BulkAsync(b => b
            .Index(_options.MessageIndexName)
            .IndexMany(docs), cancellationToken);

        if (!response.IsValidResponse)
        {
            _logger.LogWarning("批量索引消息失败: {Error}", response.DebugInformation);
        }
        else
        {
            _logger.LogDebug("批量索引消息完成，数量: {Count}", messages.Count());
        }
    }

    /// <inheritdoc />
    public async Task<SessionSearchResultDto> SearchSessionsAsync(
        string userId,
        string query,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndicesExistAsync(cancellationToken);

        try
        {
            var response = await _client.SearchAsync<SessionDocument>(s => s
                .Indices(_options.SessionIndexName)
                .From(skip)
                .Size(take)
                .Query(q => q
                    .Bool(b => b
                        .Must(
                            m => m.Term(t => t.Field(f => f.UserId).Value(userId)),
                            m => m.Bool(bq => bq
                                .Should(
                                    sq => sq.Match(mm => mm.Field(f => f.Title).Query(query).Fuzziness(new Fuzziness("AUTO"))),
                                    sq => sq.Match(mm => mm.Field(f => f.LastMessagePreview).Query(query).Fuzziness(new Fuzziness("AUTO")))
                                )
                                .MinimumShouldMatch(1)
                            )
                        )
                    )
                )
                .Sort(so => so
                    .Field(f => f.UpdatedAt, fs => fs.Order(SortOrder.Desc))
                )
                .Highlight(h => h
                    .PreTags("<em>")
                    .PostTags("</em>")
                    .Fields(fields => fields
                        .Add(doc => doc.Title, highlightField => highlightField
                            .FragmentSize(100)
                            .NumberOfFragments(0) // 0 表示高亮整个字段，不分片
                        )
                        .Add(doc => doc.LastMessagePreview, highlightField => highlightField
                            .FragmentSize(200)
                            .NumberOfFragments(1)
                        )
                    )
                ), cancellationToken);

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("搜索会话失败: {Error}", response.DebugInformation);
                return new SessionSearchResultDto();
            }

            // 处理高亮结果
            var sessions = new List<SessionSearchDocumentDto>();
            foreach (var hit in response.Hits)
            {
                if (hit.Source == null) continue;
                var session = MapToDto(hit.Source);
                
                // 应用高亮结果
                if (hit.Highlight != null)
                {
                    // 注意：Elasticsearch 返回的字段名通常是首字母小写
                    if (hit.Highlight.TryGetValue("title", out var titleHighlights) && titleHighlights.Any())
                    {
                        session.Title = string.Join(" ... ", titleHighlights);
                    }
                    
                    if (hit.Highlight.TryGetValue("lastMessagePreview", out var previewHighlights) && previewHighlights.Any())
                    {
                        session.LastMessagePreview = string.Join(" ... ", previewHighlights);
                    }
                }
                
                sessions.Add(session);
            }

            return new SessionSearchResultDto
            {
                Sessions = sessions,
                Total = response.Total
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索会话时发生错误");
            return new SessionSearchResultDto();
        }
    }

    /// <inheritdoc />
    public async Task<List<string>> SearchSessionsByMessageContentAsync(
        string userId,
        string query,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndicesExistAsync(cancellationToken);

        try
        {
            var response = await _client.SearchAsync<MessageDocument>(s => s
                .Indices(_options.MessageIndexName)
                .From(skip)
                .Size(take * 5) // 获取更多消息以便聚合
                .Query(q => q
                    .Bool(b => b
                        .Must(
                            m => m.Term(t => t.Field(f => f.UserId).Value(userId)),
                            m => m.Match(mm => mm
                                .Field(f => f.Content)
                                .Query(query)
                                .Fuzziness(new Fuzziness("AUTO"))
                            )
                        )
                    )
                )
                .Aggregations(a => a
                    .Add("unique_sessions", agg => agg
                        .Terms(t => t
                            .Field("sessionId")
                            .Size(take)
                        )
                    )
                )
                .Highlight(h => h
                    .PreTags("<em>")
                    .PostTags("</em>")
                    .Fields(fields => fields
                        .Add(doc => doc.Content, highlightField => highlightField
                            .FragmentSize(200)
                            .NumberOfFragments(3)
                        )
                    )
                )
                .Source(new Elastic.Clients.Elasticsearch.Core.Search.SourceConfig(true))
            , cancellationToken);

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("通过消息搜索会话失败: {Error}", response.DebugInformation);
                return [];
            }

            // 从聚合结果中提取唯一的 SessionId
            var sessionIds = response.Documents
                .Select(d => d.SessionId)
                .Distinct()
                .Skip(skip)
                .Take(take)
                .ToList();

            return sessionIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "通过消息搜索会话时发生错误");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            // 先检查索引是否存在，如果不存在则无需删除
            var indexExists = await _client.Indices.ExistsAsync(_options.SessionIndexName, cancellationToken);
            if (!indexExists.Exists)
            {
                _logger.LogDebug("会话索引 {IndexName} 不存在，跳过删除操作: {SessionId}", _options.SessionIndexName, sessionId);
                return;
            }

            var response = await _client.DeleteAsync(_options.SessionIndexName, sessionId, cancellationToken);

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("删除会话索引失败: {SessionId}, Error: {Error}", sessionId, response.DebugInformation);
            }
            else
            {
                _logger.LogDebug("会话索引已删除: {SessionId}", sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除会话索引时发生错误: {SessionId}", sessionId);
        }
    }

    /// <inheritdoc />
    public async Task DeleteSessionMessagesAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            // 先检查索引是否存在，如果不存在则无需删除
            var indexExists = await _client.Indices.ExistsAsync(_options.MessageIndexName, cancellationToken);
            if (!indexExists.Exists)
            {
                _logger.LogDebug("消息索引 {IndexName} 不存在，跳过删除操作: {SessionId}", _options.MessageIndexName, sessionId);
                return;
            }

            var response = await _client.DeleteByQueryAsync<MessageDocument>(_options.MessageIndexName, d => d
                .Query(q => q
                    .Term(t => t.Field(f => f.SessionId).Value(sessionId))
                ), cancellationToken);

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("删除会话消息索引失败: {SessionId}, Error: {Error}", sessionId, response.DebugInformation);
            }
            else
            {
                _logger.LogDebug("会话消息索引已删除: {SessionId}, 删除数量: {Count}", sessionId, response.Deleted);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除会话消息索引时发生错误: {SessionId}", sessionId);
        }
    }

    /// <inheritdoc />
    public async Task DeleteMessageAsync(string messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            var indexExists = await _client.Indices.ExistsAsync(_options.MessageIndexName, cancellationToken);
            if (!indexExists.Exists)
            {
                _logger.LogDebug("消息索引 {IndexName} 不存在，跳过删除操作: {MessageId}", _options.MessageIndexName, messageId);
                return;
            }

            var response = await _client.DeleteAsync(_options.MessageIndexName, messageId, cancellationToken);

            if (!response.IsValidResponse && response.Result != Result.NotFound)
            {
                _logger.LogWarning("删除消息索引失败: {MessageId}, Error: {Error}", messageId, response.DebugInformation);
            }
            else
            {
                _logger.LogDebug("消息索引已删除: {MessageId}", messageId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除消息索引时发生错误: {MessageId}", messageId);
        }
    }

    /// <inheritdoc />
    public async Task DeleteMessagesAsync(IEnumerable<string> messageIds, CancellationToken cancellationToken = default)
    {
        var ids = messageIds?.ToList();
        if (ids == null || ids.Count == 0) return;

        try
        {
            var indexExists = await _client.Indices.ExistsAsync(_options.MessageIndexName, cancellationToken);
            if (!indexExists.Exists)
            {
                _logger.LogDebug("消息索引 {IndexName} 不存在，跳过批量删除操作", _options.MessageIndexName);
                return;
            }
            
            // 使用 BulkAsync 构建批量删除操作
            var response = await _client.BulkAsync(b =>
            {
                foreach (var id in ids)
                {
                    b.Delete<MessageDocument>(d => d
                        .Index(_options.MessageIndexName)
                        .Id(id));
                }
            }, cancellationToken);

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("批量删除消息索引失败: {Error}", response.DebugInformation);
            }
            else
            {
                _logger.LogDebug("批量删除消息索引完成，数量: {Count}", ids.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量删除消息索引时发生错误");
        }
    }

    #region Private Mapping Methods

    private static SessionDocument MapToDocument(SessionSearchDocumentDto dto)
    {
        return new SessionDocument
        {
            Id = dto.Id,
            UserId = dto.UserId,
            Title = dto.Title,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt,
            LastMessagePreview = dto.LastMessagePreview,
            MessageCount = dto.MessageCount
        };
    }

    private static MessageDocument MapToDocument(MessageSearchDocumentDto dto)
    {
        return new MessageDocument
        {
            Id = dto.Id,
            SessionId = dto.SessionId,
            UserId = dto.UserId,
            Role = dto.Role,
            Content = dto.Content,
            CreatedAt = dto.CreatedAt
        };
    }

    private static SessionSearchDocumentDto MapToDto(SessionDocument doc)
    {
        return new SessionSearchDocumentDto
        {
            Id = doc.Id,
            UserId = doc.UserId,
            Title = doc.Title,
            CreatedAt = doc.CreatedAt,
            UpdatedAt = doc.UpdatedAt,
            LastMessagePreview = doc.LastMessagePreview,
            MessageCount = doc.MessageCount
        };
    }

    #endregion
}
