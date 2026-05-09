using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Text.Json;
using System.Threading.Channels;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 工具结果块执行协调器。
/// 负责识别联网搜索/网页读取类交互块，并调用对应插件回填结果。
/// </summary>
public sealed class ToolBlockExecutionCoordinator
{
    private readonly IKernelService _kernelService;
    private readonly ILogger<ToolBlockExecutionCoordinator> _logger;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public ToolBlockExecutionCoordinator(
        IKernelService kernelService,
        ILogger<ToolBlockExecutionCoordinator> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <summary>
    /// 判断当前块是否属于工具结果回填请求。
    /// </summary>
    public bool CanHandle(BlockDto block)
    {
        return IsSearchWebRequestBlock(block)
            || IsAdvancedSearchRequestBlock(block)
            || IsWebpageRequestBlock(block);
    }

    /// <summary>
    /// 执行交互块对应的工具调用，并回写结果块。
    /// </summary>
    public async Task HandleAsync(
        BlockDto requestBlock,
        Guid providerId,
        Guid messageId,
        Guid sessionId,
        ChannelWriter<BlockDto> blockWriter,
        CancellationToken cancellationToken)
    {
        if (IsSearchWebRequestBlock(requestBlock))
        {
            await EmitWebSearchResultBlockAsync(
                requestBlock,
                providerId,
                messageId,
                sessionId,
                blockWriter,
                cancellationToken);
            return;
        }

        if (IsAdvancedSearchRequestBlock(requestBlock))
        {
            await EmitAdvancedSearchResultBlockAsync(
                requestBlock,
                providerId,
                messageId,
                sessionId,
                blockWriter,
                cancellationToken);
            return;
        }

        if (IsWebpageRequestBlock(requestBlock))
        {
            await EmitWebpageResultBlockAsync(
                requestBlock,
                providerId,
                messageId,
                sessionId,
                blockWriter,
                cancellationToken);
        }
    }

    private static bool IsSearchWebRequestBlock(BlockDto block)
    {
        if (block.BlockType != BlockType.InteractiveCard)
        {
            return false;
        }

        var cardType = ToolBlockMetadataConstants.NormalizeCardType(GetMetadataString(block.Metadata, ToolBlockMetadataConstants.CardType));
        var status = ToolBlockMetadataConstants.NormalizeStatus(GetMetadataString(block.Metadata, ToolBlockMetadataConstants.Status), string.Empty);
        return cardType == ToolBlockMetadataConstants.CardTypeSearch && status == ToolBlockMetadataConstants.StatusLoading;
    }

    private static bool IsAdvancedSearchRequestBlock(BlockDto block)
    {
        if (block.BlockType != BlockType.InteractiveCard)
        {
            return false;
        }

        var cardType = ToolBlockMetadataConstants.NormalizeCardType(GetMetadataString(block.Metadata, ToolBlockMetadataConstants.CardType));
        var status = ToolBlockMetadataConstants.NormalizeStatus(GetMetadataString(block.Metadata, ToolBlockMetadataConstants.Status), string.Empty);
        return cardType == ToolBlockMetadataConstants.CardTypeAdvancedSearch && status == ToolBlockMetadataConstants.StatusLoading;
    }

    private static bool IsWebpageRequestBlock(BlockDto block)
    {
        if (block.BlockType != BlockType.InteractiveCard)
        {
            return false;
        }

        var cardType = ToolBlockMetadataConstants.NormalizeCardType(GetMetadataString(block.Metadata, ToolBlockMetadataConstants.CardType));
        var status = ToolBlockMetadataConstants.NormalizeStatus(GetMetadataString(block.Metadata, ToolBlockMetadataConstants.Status), string.Empty);
        return cardType == ToolBlockMetadataConstants.CardTypeWebpage && status == ToolBlockMetadataConstants.StatusLoading;
    }

    private static string? GetMetadataString(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        return value.ToString();
    }

    private static int GetMetadataInt(Dictionary<string, object>? metadata, string key, int defaultValue)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value) || value == null)
        {
            return defaultValue;
        }

        return int.TryParse(value.ToString(), out var parsed) ? parsed : defaultValue;
    }

    private static bool IsGitRepositoryQuery(string query)
    {
        var trimmed = query.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        return host.Contains("github.com")
            || host.Contains("gitlab")
            || host.Contains("gitea")
            || host.Contains("gitingest.com")
            || host.Contains("bitbucket.org");
    }

    private static int ParseResultCount(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return 0;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("resultCount", out var countEl) && countEl.TryGetInt32(out var n))
            {
                return n;
            }

            if (root.TryGetProperty("results", out var resultsEl) && resultsEl.ValueKind == JsonValueKind.Array)
            {
                return resultsEl.GetArrayLength();
            }

            if (root.ValueKind == JsonValueKind.Array)
            {
                return root.GetArrayLength();
            }
        }
        catch
        {
            // JSON 解析失败时静默返回 0
        }

        return 0;
    }

    private static int ParseContentLength(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return 0;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("content", out var contentEl))
            {
                var content = contentEl.GetString();
                return content?.Length ?? 0;
            }
        }
        catch
        {
            // JSON 解析失败时静默返回 0
        }

        return 0;
    }

    private static string ExtractDomain(string url)
    {
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return uri.Host;
            }
        }
        catch
        {
        }

        return url.Length > 80 ? url[..80] + "..." : url;
    }

    private async Task EmitWebSearchResultBlockAsync(
        BlockDto requestBlock,
        Guid providerId,
        Guid messageId,
        Guid sessionId,
        ChannelWriter<BlockDto> blockWriter,
        CancellationToken cancellationToken)
    {
        var query = GetMetadataString(requestBlock.Metadata, ToolBlockMetadataConstants.Query) ?? string.Empty;
        var maxResults = GetMetadataInt(requestBlock.Metadata, ToolBlockMetadataConstants.MaxResults, 5);

        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        if (IsGitRepositoryQuery(query))
        {
            _logger.LogWarning(
                "[AI.Chat] Blocked <search_web> for Git repo URL, use repo-parser Skill instead | Query={Query} MessageId={MessageId}",
                query,
                messageId);
            return;
        }

        await ThinkingContext.EmitAsync($"🔎 正在进行联网搜索: {query}");
        await ThinkingContext.EmitAsync("📡 正在查询搜索引擎，请稍候...");

        var resultsJson = await ExecuteWebSearchAsync(providerId, query, maxResults, cancellationToken);
        var resultCount = ParseResultCount(resultsJson);
        var countMsg = resultCount > 0 ? $"📋 找到 {resultCount} 条结果，正在整理..." : "⚠️ 未找到相关结果";
        await ThinkingContext.EmitAsync(countMsg);

        await blockWriter.WriteAsync(new BlockDto
        {
            BlockType = BlockType.InteractiveCard,
            Content = resultsJson,
            MessageId = messageId,
            SessionId = sessionId,
            IsLast = false,
            Metadata = new Dictionary<string, object>
            {
                { ToolBlockMetadataConstants.CardType, ToolBlockMetadataConstants.CardTypeSearch },
                { ToolBlockMetadataConstants.Query, query }
            }
        }, cancellationToken);

        await ThinkingContext.EmitAsync("✅ 搜索完成，正在整理结果...");
    }

    private async Task EmitAdvancedSearchResultBlockAsync(
        BlockDto requestBlock,
        Guid providerId,
        Guid messageId,
        Guid sessionId,
        ChannelWriter<BlockDto> blockWriter,
        CancellationToken cancellationToken)
    {
        var query = GetMetadataString(requestBlock.Metadata, ToolBlockMetadataConstants.Query) ?? string.Empty;
        var maxResults = GetMetadataInt(requestBlock.Metadata, ToolBlockMetadataConstants.MaxResults, 5);

        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        if (IsGitRepositoryQuery(query))
        {
            _logger.LogWarning(
                "[AI.Chat] Blocked <advanced_search> for Git repo URL, use repo-parser Skill instead | Query={Query} MessageId={MessageId}",
                query,
                messageId);
            return;
        }

        await ThinkingContext.EmitAsync($"🔍 正在进行高级搜索: {query}");
        await ThinkingContext.EmitAsync("📡 正在从搜索引擎获取相关链接...");

        var urlListJson = await ExecuteWebSearchAsync(providerId, query, maxResults, cancellationToken);
        var urlCount = ParseResultCount(urlListJson);

        if (urlCount > 0)
        {
            await ThinkingContext.EmitAsync($"📋 找到 {urlCount} 个相关网页，正在并发深度读取全文内容...");
        }
        else
        {
            await ThinkingContext.EmitAsync("⚠️ 搜索引擎未找到结果，尝试备用策略...");
        }

        var resultsJson = await ExecuteAdvancedSearchAsync(providerId, query, maxResults, cancellationToken);
        var readCount = ParseResultCount(resultsJson);
        var doneMsg = readCount > 0
            ? $"✅ 成功深度阅读 {readCount} 个网页，正在整理结果..."
            : "⚠️ 高级搜索未能读取网页内容";
        await ThinkingContext.EmitAsync(doneMsg);

        await blockWriter.WriteAsync(new BlockDto
        {
            BlockType = BlockType.InteractiveCard,
            Content = resultsJson,
            MessageId = messageId,
            SessionId = sessionId,
            IsLast = false,
            Metadata = new Dictionary<string, object>
            {
                { ToolBlockMetadataConstants.CardType, ToolBlockMetadataConstants.CardTypeAdvancedSearch },
                { ToolBlockMetadataConstants.Query, query }
            }
        }, cancellationToken);

        await ThinkingContext.EmitAsync("✅ 高级搜索完成，正在整理结果...");
    }

    private async Task EmitWebpageResultBlockAsync(
        BlockDto requestBlock,
        Guid providerId,
        Guid messageId,
        Guid sessionId,
        ChannelWriter<BlockDto> blockWriter,
        CancellationToken cancellationToken)
    {
        var url = GetMetadataString(requestBlock.Metadata, ToolBlockMetadataConstants.Url) ?? string.Empty;
        var method = GetMetadataString(requestBlock.Metadata, ToolBlockMetadataConstants.Method) ?? "auto";

        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        var domain = ExtractDomain(url);
        var readerLabel = method == "auto" ? "自动选择最优阅读器" : method;

        await ThinkingContext.EmitAsync($"📄 正在读取网页: {domain}");
        await ThinkingContext.EmitAsync($"🌐 使用 [{readerLabel}] 读取 {domain}，正在将网页转为 Markdown...");

        var resultJson = await ExecuteWebpageReadAsync(providerId, url, method, cancellationToken);
        var contentLength = ParseContentLength(resultJson);
        var readDoneMsg = contentLength > 0
            ? $"✅ 读取完成，获取到约 {contentLength / 1000.0:F1}K 字符的网页内容"
            : "⚠️ 网页读取完成，但内容为空";
        await ThinkingContext.EmitAsync(readDoneMsg);

        await blockWriter.WriteAsync(new BlockDto
        {
            BlockType = BlockType.InteractiveCard,
            Content = resultJson,
            MessageId = messageId,
            SessionId = sessionId,
            IsLast = false,
            Metadata = new Dictionary<string, object>
            {
                { ToolBlockMetadataConstants.CardType, ToolBlockMetadataConstants.CardTypeWebpage },
                { ToolBlockMetadataConstants.Url, url },
                { ToolBlockMetadataConstants.Method, method }
            }
        }, cancellationToken);

        await ThinkingContext.EmitAsync("✅ 网页读取完成，正在整理内容...");
    }

    private async Task<string> ExecuteWebSearchAsync(
        Guid providerId,
        string query,
        int count,
        CancellationToken cancellationToken)
    {
        try
        {
            var kernel = await _kernelService.GetKernelAsync(providerId, cancellationToken);

            if (!kernel.Plugins.TryGetPlugin("WebSearchPlugin", out _))
            {
                _logger.LogWarning("[AI.Chat] AI 聊天： WebSearchPlugin not registered in Kernel");
                return JsonSerializer.Serialize(new { success = false, error = "WebSearchPlugin 未注册" });
            }

            var arguments = new KernelArguments
            {
                ["query"] = query,
                ["count"] = count
            };

            var result = await kernel.InvokeAsync<string>("WebSearchPlugin", "Search", arguments, cancellationToken);
            if (string.IsNullOrWhiteSpace(result))
            {
                return JsonSerializer.Serialize(new { success = false, error = "搜索结果为空" });
            }

            try
            {
                using var doc = JsonDocument.Parse(result);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("results", out var results))
                {
                    return results.GetRawText();
                }
            }
            catch
            {
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AI.Chat] WebSearchPlugin error | Query={Query}", query);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    private async Task<string> ExecuteAdvancedSearchAsync(
        Guid providerId,
        string query,
        int count,
        CancellationToken cancellationToken)
    {
        try
        {
            var kernel = await _kernelService.GetKernelAsync(providerId, cancellationToken);

            if (!kernel.Plugins.TryGetPlugin("WebSearchPlugin", out _))
            {
                _logger.LogWarning("[AI.Chat] AI 聊天： WebSearchPlugin not registered in Kernel");
                return JsonSerializer.Serialize(new { success = false, error = "WebSearchPlugin 未注册" });
            }

            var arguments = new KernelArguments
            {
                ["query"] = query,
                ["count"] = count
            };

            var result = await kernel.InvokeAsync<string>("WebSearchPlugin", "AdvancedSearch", arguments, cancellationToken);
            if (string.IsNullOrWhiteSpace(result))
            {
                return JsonSerializer.Serialize(new { success = false, error = "高级搜索结果为空" });
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AI.Chat] WebSearchPlugin error | AdvancedSearch Query={Query}", query);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    private async Task<string> ExecuteWebpageReadAsync(
        Guid providerId,
        string url,
        string method,
        CancellationToken cancellationToken)
    {
        try
        {
            var kernel = await _kernelService.GetKernelAsync(providerId, cancellationToken);

            if (!kernel.Plugins.TryGetPlugin("WebSearchPlugin", out _))
            {
                _logger.LogWarning("[AI.Chat] AI 聊天： WebSearchPlugin not registered in Kernel");
                return JsonSerializer.Serialize(new { success = false, error = "WebSearchPlugin 未注册" });
            }

            var arguments = new KernelArguments
            {
                ["url"] = url,
                ["method"] = method
            };

            var result = await kernel.InvokeAsync<string>("WebSearchPlugin", "ReadWebpage", arguments, cancellationToken);
            if (string.IsNullOrWhiteSpace(result))
            {
                return JsonSerializer.Serialize(new { success = false, error = "网页读取结果为空" });
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AI.Chat] WebSearchPlugin error | ReadWebpage Url={Url}", url);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}
