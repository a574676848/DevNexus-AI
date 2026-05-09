using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Configuration;
using DevNexus.Domain.Entities;
using DevNexus.Shared.DTOs;
using DevNexus.Infrastructure.Models;
using DevNexus.Infrastructure.Services.LLM;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.KernelMemory;

namespace DevNexus.Infrastructure.Services.Memory;

/// <summary>
/// 用户记忆服务实现
/// 管理 PostgreSQL 中的语义记忆和 Qdrant 中的情境记忆
/// </summary>
public class UserMemoryService : IUserMemoryService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IKernelMemory _kernelMemory;
    private readonly IEmbeddingProviderFactory _embeddingFactory;
    private readonly ILogger<UserMemoryService> _logger;
    private readonly QdrantOptions _qdrantOptions;

    /// <summary>
    /// 情境记忆索引名称
    /// </summary>
    private string MemoryIndex => string.IsNullOrWhiteSpace(_qdrantOptions.MemoryCollectionName)
        ? "user_episodic_memory"
        : _qdrantOptions.MemoryCollectionName;

    public UserMemoryService(
        ApplicationDbContext dbContext,
        IKernelMemory kernelMemory,
        IEmbeddingProviderFactory embeddingFactory,
        IOptions<QdrantOptions> qdrantOptions,
        ILogger<UserMemoryService> logger)
    {
        _dbContext = dbContext;
        _kernelMemory = kernelMemory;
        _embeddingFactory = embeddingFactory;
        _qdrantOptions = qdrantOptions.Value;
        _logger = logger;
    }

    #region 显性语义记忆 (UserFacts)

    /// <inheritdoc />
    public async Task<List<UserFactDto>> GetUserFactsAsync(
        Guid userId,
        int minConfidence = 3,
        CancellationToken cancellationToken = default)
    {
        var facts = await _dbContext.UserFacts
            .Where(f => f.UserId == userId && f.ConfidenceScore >= minConfidence)
            .OrderByDescending(f => f.IsPinned)
            .ThenByDescending(f => f.ConfidenceScore)
            .ThenByDescending(f => f.UpdatedAt)
            .Select(f => MapToDto(f))
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "[Memory] Loaded {Count} user facts for UserId={UserId} (minConfidence={MinConfidence})",
            facts.Count, userId, minConfidence);

        return facts;
    }

    /// <inheritdoc />
    public async Task<List<UserFactDto>> GetAllUserFactsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var facts = await _dbContext.UserFacts
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.IsPinned)
            .ThenByDescending(f => f.ConfidenceScore)
            .ThenByDescending(f => f.UpdatedAt)
            .Select(f => MapToDto(f))
            .ToListAsync(cancellationToken);

        return facts;
    }

    /// <inheritdoc />
    public async Task<UserFactDto> UpsertFactAsync(
        Guid userId,
        string category,
        string content,
        Guid? sourceSessionId = null,
        CancellationToken cancellationToken = default)
    {
        // 计算内容哈希用于去重
        var contentHash = ComputeHash(content.ToLowerInvariant().Trim());

        // 检查是否存在相似的事实
        var existingFact = await _dbContext.UserFacts
            .FirstOrDefaultAsync(f => 
                f.UserId == userId && 
                f.ContentHash == contentHash, 
                cancellationToken);

        if (existingFact != null)
        {
            // 如果已存在且未被固定，增加权重
            if (!existingFact.IsPinned && existingFact.ConfidenceScore < 10)
            {
                existingFact.ConfidenceScore = Math.Min(existingFact.ConfidenceScore + 1, 10);
                existingFact.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogDebug(
                    "[Memory] Increased confidence for existing fact: {FactId} -> {Score}",
                    existingFact.Id, existingFact.ConfidenceScore);
            }

            return MapToDto(existingFact);
        }

        // 检查是否存在冲突的事实（同一类别下语义相似）
        var conflictingFact = await _dbContext.UserFacts
            .FirstOrDefaultAsync(f => 
                f.UserId == userId && 
                f.Category == category &&
                !f.IsPinned,
                cancellationToken);

        if (conflictingFact != null)
        {
            // 降低旧事实权重
            if (conflictingFact.ConfidenceScore > 1)
            {
                conflictingFact.ConfidenceScore--;
                conflictingFact.UpdatedAt = DateTime.UtcNow;

                _logger.LogDebug(
                    "[Memory] Decreased confidence for conflicting fact: {FactId} -> {Score}",
                    conflictingFact.Id, conflictingFact.ConfidenceScore);
            }
        }

        // 创建新事实
        var newFact = new UserFact
        {
            UserId = userId,
            Category = category,
            Content = content,
            ContentHash = contentHash,
            SourceSessionId = sourceSessionId,
            ConfidenceScore = 1,
            IsPinned = false
        };

        _dbContext.UserFacts.Add(newFact);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[Memory] Created new user fact: Category={Category} UserId={UserId}",
            category, userId);

        return MapToDto(newFact);
    }

    /// <inheritdoc />
    public async Task<bool> TogglePinFactAsync(
        Guid userId,
        Guid factId,
        CancellationToken cancellationToken = default)
    {
        var fact = await _dbContext.UserFacts
            .FirstOrDefaultAsync(f => f.Id == factId && f.UserId == userId, cancellationToken);

        if (fact == null)
            return false;

        fact.IsPinned = !fact.IsPinned;
        fact.UpdatedAt = DateTime.UtcNow;

        // 固定的事实权重设为最高
        if (fact.IsPinned)
        {
            fact.ConfidenceScore = 10;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[Memory] Toggled pin for fact: {FactId} IsPinned={IsPinned}",
            factId, fact.IsPinned);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFactAsync(
        Guid userId,
        Guid factId,
        CancellationToken cancellationToken = default)
    {
        var fact = await _dbContext.UserFacts
            .FirstOrDefaultAsync(f => f.Id == factId && f.UserId == userId, cancellationToken);

        if (fact == null)
            return false;

        // 软删除
        fact.IsDeleted = true;
        fact.DeletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[Memory] Deleted user fact: {FactId}", factId);

        return true;
    }

    #endregion

    #region 隐性情境记忆 (Episodic - Qdrant)

    /// <inheritdoc />
    public async Task<List<EpisodicMemoryDto>> SearchEpisodicMemoriesAsync(
        Guid userId,
        string query,
        int topK = 3,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var searchResult = await _kernelMemory.SearchAsync(
                query: query,
                index: MemoryIndex,
                filters: new List<MemoryFilter>
                {
                    MemoryFilters.ByTag("userId", userId.ToString())
                },
                limit: topK,
                minRelevance: 0.4, // 降低阈值，提高召回率
                cancellationToken: cancellationToken);

            var results = searchResult.Results
                .SelectMany(r => r.Partitions.Select(p => new { Result = r, Partition = p }))
                .Select(x => new EpisodicMemoryDto
                {
                    Id = Guid.TryParse(x.Result.DocumentId, out var docId) ? docId : Guid.NewGuid(),
                    SessionId = Guid.TryParse(GetTagValue(x.Partition.Tags, "sessionId"), out var sid) ? sid : Guid.Empty,
                    Summary = x.Partition.Text,
                    Tags = GetTagValues(x.Partition.Tags, "tags"),
                    Date = DateTime.TryParse(GetTagValue(x.Partition.Tags, "date"), out var date) ? date : DateTime.UtcNow,
                    Score = (float)x.Partition.Relevance
                })
                .ToList();

            _logger.LogDebug(
                "[Memory] Searched episodic memories: UserId={UserId} Query={Query} Results={Count}",
                userId, query.Length > 30 ? query[..30] + "..." : query, results.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Memory] Failed to search episodic memories for UserId={UserId}", userId);
            return new List<EpisodicMemoryDto>();
        }
    }

    /// <inheritdoc />
    public async Task SaveEpisodicMemoryAsync(
        Guid userId,
        Guid sessionId,
        string summary,
        List<string> tags,
        CancellationToken cancellationToken = default)
    {
        var previousContext = TokenAuditContext.Current;
        try
        {
            TokenAuditContext.Current = new TokenAuditContext
            {
                OwnerType = ModelInvocationOwnerTypes.User,
                OwnerUserId = userId,
                SessionId = sessionId,
                InvocationKind = ModelInvocationKinds.Embedding,
                SceneCode = ModelInvocationSceneCodes.MemoryUserEmbedding,
                SceneCategory = ModelInvocationSceneCategories.Memory,
                ResourceType = ModelInvocationResourceTypes.Session,
                ResourceId = sessionId.ToString()
            };

            var documentId = Guid.NewGuid().ToString();
            var tagCollection = new TagCollection
            {
                { "userId", userId.ToString() },
                { "sessionId", sessionId.ToString() },
                { "date", DateTime.UtcNow.ToString("O") }
            };

            // 添加技术标签
            foreach (var tag in tags.Take(10)) // 限制标签数量
            {
                tagCollection.Add("tags", tag);
            }

            await _kernelMemory.ImportTextAsync(
                text: summary,
                documentId: documentId,
                index: MemoryIndex,
                tags: tagCollection,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "[Memory] Saved episodic memory: SessionId={SessionId} Tags={Tags}",
                sessionId, string.Join(", ", tags));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Memory] Failed to save episodic memory for SessionId={SessionId}", sessionId);
            throw;
        }
        finally
        {
            TokenAuditContext.Current = previousContext;
        }
    }

    /// <inheritdoc />
    public async Task<List<EpisodicMemoryDto>> GetMemoryTimelineAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 使用空查询获取最近的记忆
            // 注意：Kernel Memory 不支持直接分页，这里用较大的 limit 模拟
            var searchResult = await _kernelMemory.SearchAsync(
                query: "", // 空查询获取所有
                index: MemoryIndex,
                filters: new List<MemoryFilter>
                {
                    MemoryFilters.ByTag("userId", userId.ToString())
                },
                limit: page * pageSize + pageSize, // 获取足够的结果用于分页
                minRelevance: 0,
                cancellationToken: cancellationToken);

            var results = searchResult.Results
                .SelectMany(r => r.Partitions.Select(p => new { Result = r, Partition = p }))
                .Select(x => new EpisodicMemoryDto
                {
                    Id = Guid.TryParse(x.Result.DocumentId, out var docId) ? docId : Guid.NewGuid(),
                    SessionId = Guid.TryParse(GetTagValue(x.Partition.Tags, "sessionId"), out var sid) ? sid : Guid.Empty,
                    Summary = x.Partition.Text,
                    Tags = GetTagValues(x.Partition.Tags, "tags"),
                    Date = DateTime.TryParse(GetTagValue(x.Partition.Tags, "date"), out var date) ? date : DateTime.UtcNow
                })
                .OrderByDescending(m => m.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Memory] Failed to get memory timeline for UserId={UserId}", userId);
            return new List<EpisodicMemoryDto>();
        }
    }

    #endregion

    #region 记忆检索与注入

    /// <inheritdoc />
    public async Task<MemoryContext> BuildMemoryContextAsync(
        Guid userId,
        string currentQuery,
        CancellationToken cancellationToken = default)
    {
        var context = new MemoryContext();

        // 并行加载两种记忆
        var factsTask = GetUserFactsAsync(userId, minConfidence: 3, cancellationToken);
        var episodicTask = string.IsNullOrWhiteSpace(currentQuery) 
            ? Task.FromResult(new List<EpisodicMemoryDto>())
            : SearchEpisodicMemoriesAsync(userId, currentQuery, topK: 3, cancellationToken);

        await Task.WhenAll(factsTask, episodicTask);

        context.UserFacts = factsTask.Result;
        context.EpisodicMemories = episodicTask.Result;

        _logger.LogDebug(
            "[Memory] Built memory context: UserId={UserId} Facts={FactCount} Episodic={EpisodicCount}",
            userId, context.UserFacts.Count, context.EpisodicMemories.Count);

        return context;
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 将实体映射为 DTO
    /// </summary>
    private static UserFactDto MapToDto(UserFact fact)
    {
        return new UserFactDto
        {
            Id = fact.Id,
            Category = fact.Category,
            Content = fact.Content,
            SourceSessionId = fact.SourceSessionId,
            ConfidenceScore = fact.ConfidenceScore,
            IsPinned = fact.IsPinned,
            CreatedAt = fact.CreatedAt,
            UpdatedAt = fact.UpdatedAt
        };
    }

    /// <summary>
    /// 计算内容哈希（用于去重）
    /// </summary>
    private static string ComputeHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// 从 TagCollection 获取单个 Tag 值
    /// </summary>
    private static string? GetTagValue(TagCollection? tags, string key)
    {
        if (tags == null) return null;
        return tags.TryGetValue(key, out var values) && values.Count > 0 ? values.First() : null;
    }

    /// <summary>
    /// 从 TagCollection 获取多个 Tag 值
    /// </summary>
    private static List<string> GetTagValues(TagCollection? tags, string key)
    {
        if (tags == null) return new List<string>();
        return tags.TryGetValue(key, out var values) ? values.Where(v => v != null).Select(v => v!).ToList() : new List<string>();
    }

    #endregion
}
