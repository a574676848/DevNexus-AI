// using DevNexus.Domain.Abstractions via GlobalUsings
using DevNexus.Core.Extensions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// RAG 优先的 Artifact 上下文策略服务
/// 使用向量检索获取相关文档片段，而非全量注入
/// </summary>
public partial class ArtifactContextStrategy
{
    private readonly IArtifactService _artifactService;
    private readonly IKnowledgeBaseService _knowledgeBaseService;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ArtifactContextStrategy> _logger;

    /// <summary>
    /// RAG 检索配置
    /// </summary>
    public class RagConfig
    {
        /// <summary>是否启用知识库检索</summary>
        public bool EnableKnowledgeRetrieval { get; set; } = true;

        /// <summary>最大检索结果数</summary>
        public int MaxRetrievalResults { get; set; } = 10;
        
        /// <summary>最小相似度分数</summary>
        public double MinScore { get; set; } = 0.6;
        
        /// <summary>每个片段最大字符数</summary>
        public int MaxChunkChars { get; set; } = 2000;
        
        /// <summary>RAG 上下文最大 Token 预算</summary>
        public int MaxRagTokenBudget { get; set; } = 8000;
        
        /// <summary>活跃文档最大 Token 预算</summary>
        public int MaxActiveDocTokenBudget { get; set; } = 50000;
        
        /// <summary>是否包含文档索引</summary>
        public bool IncludeDocumentIndex { get; set; } = true;
    }

    public ArtifactContextStrategy(
        IArtifactService artifactService,
        IKnowledgeBaseService knowledgeBaseService,
        IDistributedCache cache,
        ILogger<ArtifactContextStrategy> logger)
    {
        _artifactService = artifactService;
        _knowledgeBaseService = knowledgeBaseService;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// 构建 RAG 优先的上下文
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="userId">用户ID（用于 RAG 检索隔离）</param>
    /// <param name="currentMessage">当前用户消息（用于 RAG 检索）</param>
    /// <param name="activeArtifactIds">用户当前活跃的文档ID列表（保留完整内容）</param>
    /// <param name="maxTokenBudget">最大 Token 预算</param>
    /// <param name="config">RAG 配置（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<string?> BuildRagContextAsync(
        Guid sessionId,
        Guid userId,
        string? currentMessage,
        IEnumerable<Guid>? activeArtifactIds,
        int maxTokenBudget,
        RagConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        config ??= new RagConfig();
        
        var contextBuilder = new StringBuilder();
        int totalTokensUsed = 0;

        // 1. 获取会话的所有 Artifacts（用于索引和活跃文档）
        var artifacts = await _artifactService.GetSessionArtifactsAsync(sessionId, cancellationToken);
        var activeIds = activeArtifactIds?.ToHashSet() ?? new HashSet<Guid>();

        // 2. 处理活跃文档（用户当前打开的文档，保留完整内容）
        var activeArtifacts = artifacts.Where(a => activeIds.Contains(a.ArtifactId)).ToList();
        
        if (activeArtifacts.Any())
        {
            contextBuilder.AppendLine("## 📌 当前活跃文档");
            contextBuilder.AppendLine("_以下是用户当前正在查看/编辑的文档，请重点关注_");
            contextBuilder.AppendLine();

            int activeTokenBudget = Math.Min(config.MaxActiveDocTokenBudget, (int)(maxTokenBudget * 0.6));
            
            foreach (var artifact in activeArtifacts)
            {
                var remainingBudget = Math.Max(0, activeTokenBudget - totalTokensUsed);
                string content = ExtractArtifactContent(artifact, currentMessage, remainingBudget);
                if (string.IsNullOrWhiteSpace(content))
                {
                    continue;
                }

                int contentTokens = EstimateTokenCount(content);
                
                if (totalTokensUsed + contentTokens > activeTokenBudget)
                {
                    // 超出预算，截断内容
                    int remainingChars = (activeTokenBudget - totalTokensUsed) * 3;
                    if (remainingChars > 500)
                    {
                        content = content.Substring(0, Math.Min(content.Length, remainingChars)) 
                            + "\n\n... [内容已截断]";
                        contentTokens = EstimateTokenCount(content);
                    }
                    else
                    {
                        _logger.LogDebug("[RAG] Skipping active artifact due to budget: {Name}", artifact.Name);
                        continue;
                    }
                }
                
                AppendDocumentContent(contextBuilder, artifact.Name, artifact.Type ?? "unknown", content);
                totalTokensUsed += contentTokens;
            }
            
            contextBuilder.AppendLine();
        }

        // 3. RAG 检索：根据当前消息检索相关文档片段
        if (config.EnableKnowledgeRetrieval && !string.IsNullOrWhiteSpace(currentMessage))
        {
            int ragBudget = Math.Min(config.MaxRagTokenBudget, maxTokenBudget - totalTokensUsed);
            
            if (ragBudget > 1000)
            {
                var ragContext = await BuildRagRetrievalContextAsync(
                    currentMessage,
                    userId,
                    ragBudget,
                    config,
                    cancellationToken);
                
                if (!string.IsNullOrEmpty(ragContext))
                {
                    contextBuilder.AppendLine("## 🔍 相关知识库内容");
                    contextBuilder.AppendLine("_以下内容从知识库中检索，与当前问题相关_");
                    contextBuilder.AppendLine();
                    contextBuilder.AppendLine(ragContext);
                    contextBuilder.AppendLine();
                    
                    totalTokensUsed += EstimateTokenCount(ragContext);
                }
            }
        }

        // 4. 文档索引（仅元数据，供 AI 知道有哪些文档可用）
        if (config.IncludeDocumentIndex && artifacts.Any())
        {
            var nonActiveArtifacts = artifacts.Where(a => !activeIds.Contains(a.ArtifactId)).ToList();
            
            if (nonActiveArtifacts.Any())
            {
                contextBuilder.AppendLine("## 📂 可用文档索引");
                contextBuilder.AppendLine("_以下文档可通过知识库检索获取详细内容_");
                contextBuilder.AppendLine();
                contextBuilder.AppendLine("| # | 文档名称 | 类型 | 大小 |");
                contextBuilder.AppendLine("|---|----------|------|------|");
                
                int index = 1;
                foreach (var artifact in nonActiveArtifacts.Take(15))
                {
                    var content = artifact.Content ?? "";
                    var estimatedTokens = EstimateTokenCount(content);
                    contextBuilder.AppendLine($"| {index++} | {artifact.Name} | {artifact.Type ?? "unknown"} | ~{estimatedTokens} tokens |");
                }
                
                if (nonActiveArtifacts.Count > 15)
                {
                    contextBuilder.AppendLine($"| ... | _{nonActiveArtifacts.Count - 15} 个文档已省略_ | | |");
                }
                
                contextBuilder.AppendLine();
            }
        }

        // 5. 添加使用说明（仅知识库检索开启时展示）
        if (config.EnableKnowledgeRetrieval && contextBuilder.Length > 0)
        {
            contextBuilder.AppendLine("---");
            contextBuilder.AppendLine("💡 **提示**: 如需查看其他文档的详细内容，请告诉我文档名称，我会从知识库中检索。");
        }

        _logger.LogDebug(
            "[RAG] Built context | SessionId={SessionId} ActiveDocs={Active} TotalTokens={Tokens}",
            sessionId, activeArtifacts.Count, totalTokensUsed);

        var result = contextBuilder.ToString();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    /// <summary>
    /// 使用 RAG 检索相关文档片段
    /// </summary>
    private async Task<string?> BuildRagRetrievalContextAsync(
        string query,
        Guid userId,
        int tokenBudget,
        RagConfig config,
        CancellationToken cancellationToken)
    {
        try
        {
            // 调用知识库语义搜索
            var chunks = await _knowledgeBaseService.SearchAsync(
                query,
                userId,
                config.MaxRetrievalResults,
                config.MinScore,
                cancellationToken);

            if (!chunks.Any())
            {
                _logger.LogDebug("[RAG] No relevant chunks found for query: {Query}", 
                    query.Length > 50 ? query[..50] + "..." : query);
                return null;
            }

            var contextBuilder = new StringBuilder();
            int usedTokens = 0;

            foreach (var chunk in chunks)
            {
                var content = chunk.Content ?? "";
                
                // 限制单个片段大小
                if (content.Length > config.MaxChunkChars)
                {
                    content = content.Substring(0, config.MaxChunkChars) + "...";
                }

                int chunkTokens = EstimateTokenCount(content);
                
                if (usedTokens + chunkTokens > tokenBudget)
                {
                    _logger.LogDebug("[RAG] Token budget reached, stopping at {Count} chunks", chunks.IndexOf(chunk));
                    break;
                }

                // 获取元数据
                var fileName = chunk.Metadata?.TryGetValue(SearchResultMetadataKeys.FileName, out var fn) == true 
                    ? fn?.ToString() ?? "Unknown" 
                    : "Unknown";
                var score = chunk.Metadata?.TryGetValue(SearchResultMetadataKeys.Score, out var s) == true 
                    ? s?.ToString() ?? "N/A" 
                    : "N/A";

                contextBuilder.AppendLine($"### 📄 来源: {fileName} (相关度: {score})");
                contextBuilder.AppendLine();
                contextBuilder.AppendLine(content);
                contextBuilder.AppendLine();

                usedTokens += chunkTokens;
            }

            _logger.LogInformation(
                "[RAG] Retrieved {Count} chunks for context | Query={Query} Tokens={Tokens}",
                chunks.Count, 
                query.Length > 30 ? query[..30] + "..." : query,
                usedTokens);

            return contextBuilder.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RAG] Failed to retrieve chunks, falling back to no RAG context");
            return null;
        }
    }

    /// <summary>
    /// 提取 Artifact 内容（处理 SmartDocument 类型）
    /// </summary>
    /// <summary>表格处理方法在 ArtifactContextStrategy.Table.cs 中</summary>
}
