using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using System.Text;
using System.Text.RegularExpressions;

namespace DevNexus.Core.Services.Chat;

public partial class StreamBlockParser
{
    private const string SearchWebEndTag = "</search_web>";
    private const string AdvancedSearchStartTag = "<advanced_search>";
    private const string AdvancedSearchEndTag = "</advanced_search>";
    private const string WebpageStartTag = "<webpage>";
    private const string WebpageEndTag = "</webpage>";

    /// <summary>
    /// 在普通文本状态中处理 <search_web> 标签
    /// </summary>
    private IEnumerable<BlockDto> ProcessSearchWebTagInText(string line)
    {
        var startIndex = line.IndexOf(SearchWebStartTag, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
        {
            yield break;
        }

        // 输出标签之前的文本
        var before = line.Substring(0, startIndex);
        if (!string.IsNullOrEmpty(before))
        {
            _textBuffer.AppendLine(before);
        }

        // flush 文本缓冲区
        if (_textBuffer.Length > 0)
        {
            yield return new BlockDto
            {
                BlockId = Guid.NewGuid(),
                BlockType = BlockType.TextDelta,
                Content = _textBuffer.ToString(),
                IsLast = false
            };
            _textBuffer.Clear();
        }

        var afterStart = line.Substring(startIndex + SearchWebStartTag.Length);
        var endIndex = afterStart.IndexOf(SearchWebEndTag, StringComparison.OrdinalIgnoreCase);

        if (endIndex >= 0)
        {
            // 标签在同一行闭合
            var inner = afterStart.Substring(0, endIndex);
            if (!string.IsNullOrEmpty(inner))
            {
                _searchBuffer.AppendLine(inner);
            }

            var requestBlock = BuildSearchWebBlock(_searchBuffer.ToString());
            _searchBuffer.Clear();
            if (requestBlock != null)
            {
                yield return requestBlock;
            }

            // 处理结束标签后的文本
            var after = afterStart.Substring(endIndex + SearchWebEndTag.Length);
            if (!string.IsNullOrEmpty(after))
            {
                _textBuffer.AppendLine(after);
            }
        }
        else
        {
            // 进入 SearchWeb 模式
            if (!string.IsNullOrEmpty(afterStart))
            {
                _searchBuffer.AppendLine(afterStart);
            }
            _state = ParserState.SearchWeb;
        }
    }

    /// <summary>
    /// 在 SearchWeb 模式中处理行
    /// </summary>
    private IEnumerable<BlockDto> ProcessSearchWebTagContent(string line)
    {
        var endIndex = line.IndexOf(SearchWebEndTag, StringComparison.OrdinalIgnoreCase);
        if (endIndex < 0)
        {
            _searchBuffer.AppendLine(line);
            yield break;
        }

        var inner = line.Substring(0, endIndex);
        if (!string.IsNullOrEmpty(inner))
        {
            _searchBuffer.AppendLine(inner);
        }

        var requestBlock = BuildSearchWebBlock(_searchBuffer.ToString());
        _searchBuffer.Clear();
        _state = ParserState.Text;
        if (requestBlock != null)
        {
            yield return requestBlock;
        }

        // 处理结束标签后的剩余文本
        var after = line.Substring(endIndex + SearchWebEndTag.Length);
        if (!string.IsNullOrEmpty(after))
        {
            _textBuffer.AppendLine(after);
        }
    }

    /// <summary>
    /// 构建 WebSearch 请求 Block（交互卡片 loading 状态）
    /// </summary>
    private BlockDto? BuildSearchWebBlock(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var maxResults = 5;
        var maxMatch = Regex.Match(raw, @"<max_results>\s*(\d+)\s*</max_results>", RegexOptions.IgnoreCase);
        if (maxMatch.Success && int.TryParse(maxMatch.Groups[1].Value, out var parsed))
        {
            maxResults = Math.Clamp(parsed, 1, 10);
        }

        var cleaned = Regex.Replace(raw, @"<max_results>[\s\S]*?</max_results>", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"<[^>]+>", "", RegexOptions.Singleline);

        var query = cleaned
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

        if (string.IsNullOrWhiteSpace(query)) return null;

        return new BlockDto
        {
            BlockType = BlockType.InteractiveCard,
            Content = string.Empty,
            IsLast = false,
            Metadata = new Dictionary<string, object>
            {
                { ToolBlockMetadataConstants.CardType, ToolBlockMetadataConstants.CardTypeSearch },
                { ToolBlockMetadataConstants.Query, query },
                { ToolBlockMetadataConstants.MaxResults, maxResults },
                { ToolBlockMetadataConstants.Status, ToolBlockMetadataConstants.StatusLoading }
            }
        };
    }

    /// <summary>
    /// 在普通文本状态中处理 <advanced_search> 标签
    /// </summary>
    private IEnumerable<BlockDto> ProcessAdvancedSearchTagInText(string line)
    {
        var startIndex = line.IndexOf(AdvancedSearchStartTag, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
        {
            yield break;
        }

        // 输出标签之前的文本
        var before = line.Substring(0, startIndex);
        if (!string.IsNullOrEmpty(before))
        {
            _textBuffer.AppendLine(before);
        }

        // flush 文本缓冲区
        if (_textBuffer.Length > 0)
        {
            yield return new BlockDto
            {
                BlockId = Guid.NewGuid(),
                BlockType = BlockType.TextDelta,
                Content = _textBuffer.ToString(),
                IsLast = false
            };
            _textBuffer.Clear();
        }

        var afterStart = line.Substring(startIndex + AdvancedSearchStartTag.Length);
        var endIndex = afterStart.IndexOf(AdvancedSearchEndTag, StringComparison.OrdinalIgnoreCase);

        if (endIndex >= 0)
        {
            // 标签在同一行闭合
            var inner = afterStart.Substring(0, endIndex);
            if (!string.IsNullOrEmpty(inner))
            {
                _advancedSearchBuffer.AppendLine(inner);
            }

            var requestBlock = BuildAdvancedSearchBlock(_advancedSearchBuffer.ToString());
            _advancedSearchBuffer.Clear();
            if (requestBlock != null)
            {
                yield return requestBlock;
            }

            // 处理结束标签后的文本
            var after = afterStart.Substring(endIndex + AdvancedSearchEndTag.Length);
            if (!string.IsNullOrEmpty(after))
            {
                _textBuffer.AppendLine(after);
            }
        }
        else
        {
            // 进入 AdvancedSearch 模式
            if (!string.IsNullOrEmpty(afterStart))
            {
                _advancedSearchBuffer.AppendLine(afterStart);
            }
            _state = ParserState.AdvancedSearch;
        }
    }

    /// <summary>
    /// 在 AdvancedSearch 模式中处理行
    /// </summary>
    private IEnumerable<BlockDto> ProcessAdvancedSearchTagContent(string line)
    {
        var endIndex = line.IndexOf(AdvancedSearchEndTag, StringComparison.OrdinalIgnoreCase);
        if (endIndex < 0)
        {
            _advancedSearchBuffer.AppendLine(line);
            yield break;
        }

        var inner = line.Substring(0, endIndex);
        if (!string.IsNullOrEmpty(inner))
        {
            _advancedSearchBuffer.AppendLine(inner);
        }

        var requestBlock = BuildAdvancedSearchBlock(_advancedSearchBuffer.ToString());
        _advancedSearchBuffer.Clear();
        _state = ParserState.Text;
        if (requestBlock != null)
        {
            yield return requestBlock;
        }

        // 处理结束标签后的剩余文本
        var after = line.Substring(endIndex + AdvancedSearchEndTag.Length);
        if (!string.IsNullOrEmpty(after))
        {
            _textBuffer.AppendLine(after);
        }
    }

    /// <summary>
    /// 在普通文本状态中处理 <webpage> 标签
    /// </summary>
    private IEnumerable<BlockDto> ProcessWebpageTagInText(string line)
    {
        var startIndex = line.IndexOf(WebpageStartTag, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
        {
            yield break;
        }

        // 输出标签之前的文本
        var before = line.Substring(0, startIndex);
        if (!string.IsNullOrEmpty(before))
        {
            _textBuffer.AppendLine(before);
        }

        // flush 文本缓冲区
        if (_textBuffer.Length > 0)
        {
            yield return new BlockDto
            {
                BlockId = Guid.NewGuid(),
                BlockType = BlockType.TextDelta,
                Content = _textBuffer.ToString(),
                IsLast = false
            };
            _textBuffer.Clear();
        }

        var afterStart = line.Substring(startIndex + WebpageStartTag.Length);
        var endIndex = afterStart.IndexOf(WebpageEndTag, StringComparison.OrdinalIgnoreCase);

        if (endIndex >= 0)
        {
            // 标签在同一行闭合
            var inner = afterStart.Substring(0, endIndex);
            if (!string.IsNullOrEmpty(inner))
            {
                _webpageBuffer.AppendLine(inner);
            }

            var requestBlock = BuildWebpageBlock(_webpageBuffer.ToString());
            _webpageBuffer.Clear();
            if (requestBlock != null)
            {
                yield return requestBlock;
            }

            // 处理结束标签后的文本
            var after = afterStart.Substring(endIndex + WebpageEndTag.Length);
            if (!string.IsNullOrEmpty(after))
            {
                _textBuffer.AppendLine(after);
            }
        }
        else
        {
            // 进入 Webpage 模式
            if (!string.IsNullOrEmpty(afterStart))
            {
                _webpageBuffer.AppendLine(afterStart);
            }
            _state = ParserState.Webpage;
        }
    }

    /// <summary>
    /// 在 Webpage 模式中处理行
    /// </summary>
    private IEnumerable<BlockDto> ProcessWebpageTagContent(string line)
    {
        var endIndex = line.IndexOf(WebpageEndTag, StringComparison.OrdinalIgnoreCase);
        if (endIndex < 0)
        {
            _webpageBuffer.AppendLine(line);
            yield break;
        }

        var inner = line.Substring(0, endIndex);
        if (!string.IsNullOrEmpty(inner))
        {
            _webpageBuffer.AppendLine(inner);
        }

        var requestBlock = BuildWebpageBlock(_webpageBuffer.ToString());
        _webpageBuffer.Clear();
        _state = ParserState.Text;
        if (requestBlock != null)
        {
            yield return requestBlock;
        }

        // 处理结束标签后的剩余文本
        var after = line.Substring(endIndex + WebpageEndTag.Length);
        if (!string.IsNullOrEmpty(after))
        {
            _textBuffer.AppendLine(after);
        }
    }

    /// <summary>
    /// 构建 AdvancedSearch 请求 Block
    /// 格式: <advanced_search>query text</advanced_search>
    /// </summary>
    private BlockDto? BuildAdvancedSearchBlock(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var maxResults = 5;
        var maxMatch = Regex.Match(raw, @"<max_results>\s*(\d+)\s*</max_results>", RegexOptions.IgnoreCase);
        if (maxMatch.Success && int.TryParse(maxMatch.Groups[1].Value, out var parsed))
        {
            maxResults = Math.Clamp(parsed, 1, 10);
        }

        var cleaned = Regex.Replace(raw, @"<max_results>[\s\S]*?</max_results>", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"<[^>]+>", "", RegexOptions.Singleline);

        var query = cleaned
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

        if (string.IsNullOrWhiteSpace(query)) return null;

        return new BlockDto
        {
            BlockType = BlockType.InteractiveCard,
            Content = string.Empty,
            IsLast = false,
            Metadata = new Dictionary<string, object>
            {
                { ToolBlockMetadataConstants.CardType, ToolBlockMetadataConstants.CardTypeAdvancedSearch },
                { ToolBlockMetadataConstants.Query, query },
                { ToolBlockMetadataConstants.MaxResults, maxResults },
                { ToolBlockMetadataConstants.Status, ToolBlockMetadataConstants.StatusLoading }
            }
        };
    }

    /// <summary>
    /// 构建 Webpage 阅读 Block
    /// 格式: <webpage>URL</webpage> 或 <webpage><method>jina</method>URL</webpage>
    /// </summary>
    private BlockDto? BuildWebpageBlock(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var method = "auto";
        var methodMatch = Regex.Match(raw, @"<method>\s*(\w+)\s*</method>", RegexOptions.IgnoreCase);
        if (methodMatch.Success)
        {
            method = methodMatch.Groups[1].Value.ToLowerInvariant();
        }

        var cleaned = Regex.Replace(raw, @"<method>[\s\S]*?</method>", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"<[^>]+>", "", RegexOptions.Singleline);

        var url = cleaned
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

        if (string.IsNullOrWhiteSpace(url)) return null;

        // 验证 URL 格式
        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            return null;
        }

        return new BlockDto
        {
            BlockType = BlockType.InteractiveCard,
            Content = string.Empty,
            IsLast = false,
            Metadata = new Dictionary<string, object>
            {
                { ToolBlockMetadataConstants.CardType, ToolBlockMetadataConstants.CardTypeWebpage },
                { ToolBlockMetadataConstants.Url, url },
                { ToolBlockMetadataConstants.Method, method },
                { ToolBlockMetadataConstants.Status, ToolBlockMetadataConstants.StatusLoading }
            }
        };
    }

    /// <summary>
    /// 判断文本是否可能是 Block 标记的起始前缀（防止跨 chunk 拆分误判）
    /// 例如 ":" 可能是 ":::chart" 被拆分后的第一个 chunk，不应提前 flush
    /// </summary>
    private static bool CouldBeBlockMarkerStart(string text)
    {
        var trimmed = text.TrimStart();
        if (trimmed.Length == 0) return false;

        // 检查是否可能是 ":::blockname" 的前缀
        if (trimmed.Length < 3)
            return ":::".StartsWith(trimmed, StringComparison.Ordinal);
        if (trimmed.StartsWith(":::", StringComparison.Ordinal))
            return true;

        // 检查是否可能是 "<search_web>" 的前缀
        const string searchTag = "<search_web>";
        if (trimmed.Length < searchTag.Length)
            return searchTag.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase);
        if (trimmed.StartsWith("<search_web", StringComparison.OrdinalIgnoreCase))
            return true;

        // 检查是否可能是 "<advanced_search>" 的前缀
        const string advancedSearchTag = "<advanced_search>";
        if (trimmed.Length < advancedSearchTag.Length)
            return advancedSearchTag.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase);
        if (trimmed.StartsWith("<advanced_search", StringComparison.OrdinalIgnoreCase))
            return true;

        // 检查是否可能是 "<webpage>" 的前缀
        const string webpageTag = "<webpage>";
        if (trimmed.Length < webpageTag.Length)
            return webpageTag.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase);
        if (trimmed.StartsWith("<webpage", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
