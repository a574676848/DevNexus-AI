using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using DevNexus.Domain.Abstractions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;

namespace DevNexus.Infrastructure.Services.Plugins;

/// <summary>
/// 知识库检索插件 (Semantic Kernel Plugin)
/// 允许 AI 自主从内部知识库中检索相关文档
/// </summary>
public class KnowledgeBasePlugin
{
    private readonly IKnowledgeBaseService _knowledgeBaseService;
    private readonly ILogger<KnowledgeBasePlugin> _logger;
    private readonly Guid _userId;

    // 此插件需要 UserId 上下文，因此最好在运行时通过 Kernel 注入或工厂创建
    // 或者，我们可以将 UserId 作为参数传给 Search 方法，但这样模型可能会困惑
    // 最佳实践：Plugin 应该是 Stateless 的，或者 State 在构造时注入
    public KnowledgeBasePlugin(
        IKnowledgeBaseService knowledgeBaseService,
        ILogger<KnowledgeBasePlugin> logger,
        Guid userId)
    {
        _knowledgeBaseService = knowledgeBaseService ?? throw new ArgumentNullException(nameof(knowledgeBaseService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _userId = userId;
    }

    /// <summary>
    /// 从知识库中搜索相关内容
    /// </summary>
    [KernelFunction, Description("从内部知识库中搜索相关的文档、手册、技术规范或项目资料")]
    public async Task<string> SearchAsync(
        [Description("用于检索的关键词或问题")] string query,
        [Description("返回结果的最大数量，默认为5")] int limit = 5
    )
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return JsonSerializer.Serialize(new { success = false, error = "Search query implies empty" });
        }

        try
        {
            _logger.LogDebug(
                "[KnowledgeBasePlugin] Searching | UserId={UserId} Query='{Query}' Limit={Limit}",
                _userId, query, limit);

            // 调用知识库服务（用户级隔离）
            var results = await _knowledgeBaseService.SearchAsync(
                query: query, 
                userId: _userId, 
                limit: limit, 
                minScore: 0.6
            );

            if (results == null || results.Count == 0)
            {
                _logger.LogDebug("[KnowledgeBasePlugin] No results found.");
                return JsonSerializer.Serialize(new 
                { 
                    success = true, 
                    message = "未在知识库中找到相关内容。", 
                    count = 0 
                });
            }

            // 格式化返回结果
            var formattedResults = results.Select((r, i) => new
            {
                index = i + 1,
                fileName = r.Metadata?.TryGetValue(SearchResultMetadataKeys.FileName, out var fn) == true ? fn?.ToString() : "Unknown",
                content = r.Content,
                score = r.Metadata?.TryGetValue(SearchResultMetadataKeys.Score, out var sc) == true ? sc : 0
            });

            _logger.LogDebug(
                "[KnowledgeBasePlugin] Found {Count} results",
                results.Count);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"找到 {results.Count} 条相关记录",
                results = formattedResults
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[KnowledgeBasePlugin] Error during search");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"知识库检索发生错误: {ex.Message}"
            });
        }
    }
}
