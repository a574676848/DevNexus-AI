using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using System.Text;
using System.Text.RegularExpressions;

namespace DevNexus.Core.Services.Chat;

public partial class StreamBlockParser
{

    /// <summary>
    /// 解析属性字符串
    /// </summary>
    private Dictionary<string, string> ParseAttributes(string attributeString)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(attributeString))
            return attributes;

        var matches = AttributePattern.Matches(attributeString);
        foreach (Match match in matches)
        {
            // 双引号匹配
            if (!string.IsNullOrEmpty(match.Groups[1].Value))
            {
                attributes[match.Groups[1].Value] = match.Groups[2].Value;
            }
            // 单引号匹配
            else if (!string.IsNullOrEmpty(match.Groups[3].Value))
            {
                attributes[match.Groups[3].Value] = match.Groups[4].Value;
            }
        }

        return attributes;
    }

    /// <summary>
    /// 构建 BlockDto
    /// </summary>
    private BlockDto BuildBlockDto(BlockContext context)
    {
        var content = context.Content.ToString().TrimEnd('\r', '\n');
        
        // 解析通用属性
        context.Attributes.TryGetValue("id", out var artifactId);
        context.Attributes.TryGetValue("version", out var versionStr);
        context.Attributes.TryGetValue("action", out var actionStr);
        context.Attributes.TryGetValue("highlight", out var highlight);
        
        var version = int.TryParse(versionStr, out var v) ? v : 1;
        var action = ParseBlockAction(actionStr);

        var block = context.BlockName.ToLowerInvariant() switch
        {
            "chart" => BuildChartBlock(context, content),
            // 自动检测：如果 :::code 块内容是完整 HTML 页面，自动路由到 HtmlPreview 渲染
            // 解决 LLM 误用 :::code 代替 :::html 输出完整网页的场景
            "code" => IsCompleteHtmlPage(content)
                ? BuildHtmlBlock(context, content)
                : BuildCodeBlock(context, content),
            "card" => BuildCardBlock(context, content),
            "html" => BuildHtmlBlock(context, content),
            "thinking" => BuildThinkingBlock(context, content),
            "ref" => BuildReferenceBlock(context, content),
            _ => new BlockDto
            {
                BlockId = Guid.NewGuid(),
                BlockType = BlockType.TextDelta,
                Content = $":::未知 Block 类型: {context.BlockName}:::\n{content}",
                IsLast = false
            }
        };
        
        // 设置通用属性
        block.ArtifactId = artifactId;
        block.Version = version;
        block.Action = action;
        block.Highlight = highlight;
        
        return block;
    }
    
    /// <summary>
    /// 解析 BlockAction
    /// </summary>
    private static BlockAction ParseBlockAction(string? actionStr)
    {
        if (string.IsNullOrEmpty(actionStr))
            return BlockAction.Create;
            
        return actionStr.ToLowerInvariant() switch
        {
            "update" => BlockAction.Update,
            "delete" => BlockAction.Delete,
            _ => BlockAction.Create
        };
    }

    /// <summary>
    /// 构建图表 Block
    /// </summary>
    private BlockDto BuildChartBlock(BlockContext context, string content)
    {
        context.Attributes.TryGetValue("type", out var chartType);
        context.Attributes.TryGetValue("title", out var title);
        context.Attributes.TryGetValue("layout", out var layout);

        return new BlockDto
        {
            BlockType = BlockType.Chart,
            Content = content,
            IsLast = false,
            Metadata = new Dictionary<string, object>
            {
                { ArtifactBlockMetadataConstants.ChartType, chartType ?? ArtifactBlockMetadataConstants.ChartTypeLine },
                { ArtifactBlockMetadataConstants.Title, title ?? ArtifactBlockMetadataConstants.DefaultChartTitle },
                { ArtifactBlockMetadataConstants.Layout, layout ?? "{}" }
            }
        };
    }

    /// <summary>
    /// 构建代码 Block（触发 Artifact）
    /// </summary>
    private BlockDto BuildCodeBlock(BlockContext context, string content)
    {
        context.Attributes.TryGetValue("lang", out var language);
        context.Attributes.TryGetValue("title", out var title);

        var artifactId = Guid.NewGuid();

        // 返回一个包含完整 Artifact 信息的 Block
        // 注意：这里简化处理，实际流式场景需要拆分为 Start/Delta/End
        return new BlockDto
        {
            BlockType = BlockType.ArtifactStart,
            Content = content,
            IsLast = false,
            Metadata = new Dictionary<string, object>
            {
                { ArtifactBlockMetadataConstants.ArtifactId, artifactId },
                { ArtifactBlockMetadataConstants.Type, ArtifactBlockMetadataConstants.TypeCode },
                { ArtifactBlockMetadataConstants.Language, language ?? ArtifactBlockMetadataConstants.LanguagePlaintext },
                { ArtifactBlockMetadataConstants.Title, title ?? ArtifactBlockMetadataConstants.DefaultCodeTitle },
                { ArtifactBlockMetadataConstants.IsComplete, true } // 标记为完整内容，前端可一次性渲染
            }
        };
    }

    /// <summary>
    /// 构建交互卡片 Block
    /// </summary>
    private BlockDto BuildCardBlock(BlockContext context, string content)
    {
        context.Attributes.TryGetValue("type", out var cardType);
        
        // 优先使用属性中指定的 actionId，用于持久化恢复
        context.Attributes.TryGetValue("actionId", out var attrActionId);
        var actionId = Guid.TryParse(attrActionId, out var parsedActionId) 
            ? parsedActionId 
            : Guid.NewGuid();

        var metadata = new Dictionary<string, object>
        {
            { ToolBlockMetadataConstants.CardType, ToolBlockMetadataConstants.NormalizeCardType(cardType) },
            { ToolBlockMetadataConstants.ActionId, actionId }
        };

        // 复制所有属性到 Metadata
        foreach (var kvp in context.Attributes)
        {
            if (!string.Equals(kvp.Key, "type", StringComparison.OrdinalIgnoreCase) && 
                !string.Equals(kvp.Key, "actionId", StringComparison.OrdinalIgnoreCase))
            {
                // 处理 boolean 类型，以便前端正确识别已处理状态
                var val = kvp.Value?.ToLowerInvariant();
                if (val == "true")
                    metadata[kvp.Key] = true;
                else if (val == "false")
                    metadata[kvp.Key] = false;
                else
                    metadata[kvp.Key] = kvp.Value ?? string.Empty;
            }
        }

        return new BlockDto
        {
            BlockType = BlockType.InteractiveCard,
            Content = content,
            IsLast = false,
            Metadata = metadata
        };
    }

    /// <summary>
    /// 检测内容是否为完整的 HTML 页面
    /// 用于自动将 LLM 误用 :::code 输出的 HTML 页面路由到 HtmlPreview 渲染
    /// </summary>
    private static bool IsCompleteHtmlPage(string content)
    {
        var trimmed = content.TrimStart();
        return trimmed.StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 构建 HTML 预览 Block
    /// </summary>
    private BlockDto BuildHtmlBlock(BlockContext context, string content)
    {
        context.Attributes.TryGetValue("title", out var title);

        return new BlockDto
        {
            BlockType = BlockType.ArtifactStart,
            Content = content,
            IsLast = false,
            Metadata = new Dictionary<string, object>
            {
                { ArtifactBlockMetadataConstants.ArtifactId, Guid.NewGuid() },
                { ArtifactBlockMetadataConstants.Type, ArtifactBlockMetadataConstants.TypeHtml },
                { ArtifactBlockMetadataConstants.Title, title ?? ArtifactBlockMetadataConstants.DefaultHtmlTitle },
                { ArtifactBlockMetadataConstants.IsComplete, true }
            }
        };
    }

    /// <summary>
    /// 构建思维链 Block
    /// </summary>
    private BlockDto BuildThinkingBlock(BlockContext context, string content)
    {
        context.Attributes.TryGetValue("collapsed", out var collapsed);

        return new BlockDto
        {
            BlockType = BlockType.Thinking,
            Content = content,
            IsLast = false,
            Metadata = new Dictionary<string, object>
            {
                { FeedbackBlockMetadataConstants.Collapsed, collapsed?.ToLowerInvariant() == "true" }
            }
        };
    }
    
    /// <summary>
    /// 构建引用 Block
    /// </summary>
    private BlockDto BuildReferenceBlock(BlockContext context, string content)
    {
        // id 属性会在 BuildBlockDto 中统一设置到 ArtifactId
        return new BlockDto
        {
            BlockType = BlockType.Reference,
            Content = content,
            IsLast = false
        };
    }

    /// <summary>
    /// 刷新缓冲区，返回剩余未处理的内容
    /// </summary>
    /// <returns>剩余的 Block（如果有）</returns>
    public IEnumerable<BlockDto> Flush()
    {
        // 处理行缓冲区中的剩余内容
        if (_lineBuffer.Length > 0)
        {
            var remainingLine = _lineBuffer.ToString();
            _lineBuffer.Clear();

            foreach (var block in ProcessLine(remainingLine))
            {
                yield return block;
            }
        }

        // 处理文本缓冲区
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

        // 处理未完成的 Block（安全包装为 Markdown 代码块，防止原始 HTML/CSS 注入页面）
        if (_currentBlock != null)
        {
            var blockContent = _currentBlock.Content.ToString().TrimEnd('\r', '\n');
            var blockName = _currentBlock.BlockName.ToLowerInvariant();

            // 根据 Block 类型选择合适的语言标识
            var lang = blockName switch
            {
                "code" => _currentBlock.Attributes.TryGetValue("lang", out var l) ? l : "text",
                "html" => "html",
                "chart" => "json",
                _ => "text"
            };

            // 使用 Markdown 代码围栏安全包装，防止 <style>/<script> 等标签注入页面 DOM
            var incompleteContent = $"```{lang}\n{blockContent}\n```\n";
            yield return new BlockDto
            {
                BlockId = Guid.NewGuid(),
                BlockType = BlockType.TextDelta,
                Content = incompleteContent,
                IsLast = false
            };
            _currentBlock = null;
            _state = ParserState.Text;
        }

        // 处理未完成的 <search_web>（尝试生成搜索请求）
        if (_state == ParserState.SearchWeb && _searchBuffer.Length > 0)
        {
            var requestBlock = BuildSearchWebBlock(_searchBuffer.ToString());
            _searchBuffer.Clear();
            _state = ParserState.Text;
            if (requestBlock != null)
            {
                yield return requestBlock;
            }
        }
    }

    /// <summary>
    /// 重置解析器状态
    /// </summary>
    public void Reset()
    {
        _state = ParserState.Text;
        _currentBlock = null;
        _textBuffer.Clear();
        _lineBuffer.Clear();
    }
}
