using System.Text.RegularExpressions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Client.Shared.Components.Chat;

/// <summary>
/// 消息内容解析器，负责读取结构化正文、思考内容并过滤块标记。
/// </summary>
public static class MessageContentParser
{
    /// <summary>
    /// 解析消息内容，thinking 只来自结构化字段。
    /// </summary>
    public static (string DisplayedContent, List<string> Thoughts) ParseContent(ChatMessageDto message)
    {
        if (ChatConstants.IsUserSender(message.SenderType))
        {
            return (message.TextContent ?? message.Content, new List<string>());
        }

        var displayedContent = StripBlockMarkers(message.TextContent ?? message.Content);
        return (displayedContent, SplitThoughts(message.ThinkingContent));
    }

    private static string StripBlockMarkers(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        var displayedContent = System.Text.RegularExpressions.Regex.Replace(
            content,
            @":::(\w+(?:-\w+)*)(?:\{[^}]*\})?\s*[\r\n]*[\s\S]*?[\r\n]*:::",
            "",
            System.Text.RegularExpressions.RegexOptions.Multiline);

        return System.Text.RegularExpressions.Regex.Replace(displayedContent, @"\n{3,}", "\n\n").Trim();
    }

    private static List<string> SplitThoughts(string? thoughtContent)
    {
        if (string.IsNullOrWhiteSpace(thoughtContent))
        {
            return new List<string>();
        }

        var steps = thoughtContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(step => step.Trim())
            .Where(step => !string.IsNullOrWhiteSpace(step))
            .ToList();

        return steps.Count > 0 ? steps : new List<string> { thoughtContent.Trim() };
    }

    /// <summary>
    /// 从 Artifacts 中提取图表数据并转换为 BlockDto 列表
    /// </summary>
    public static List<BlockDto> GetChartBlocksFromArtifacts(ChatMessageDto message)
    {
        // 优先使用已有的 ChartBlocks
        if (message.ChartBlocks != null && message.ChartBlocks.Any())
        {
            return message.ChartBlocks;
        }
        
        // 从 Artifacts 中提取 chart 类型
        if (message.Artifacts == null || !message.Artifacts.Any())
        {
            return new List<BlockDto>();
        }
        
        return message.Artifacts
            .Where(a => a.Type?.ToLower() == "chart")
            .Select(a => new BlockDto
            {
                BlockType = BlockType.Chart,
                Content = a.Content,
                MessageId = a.MessageId,
                Metadata = new Dictionary<string, object>
                {
                    { ArtifactBlockMetadataConstants.Title, a.Name ?? ArtifactBlockMetadataConstants.DefaultChartTitle },
                    { ArtifactBlockMetadataConstants.ChartType, ArtifactBlockMetadataConstants.ChartTypeAuto }
                }
            })
            .ToList();
    }

    /// <summary>
    /// 从 Artifacts 中提取交互卡片数据并转换为 BlockDto 列表
    /// 注意：如果 OrderedBlocks 中已有 InteractiveCard，则返回空列表避免重复
    /// </summary>
    public static List<BlockDto> GetInteractiveBlocksFromArtifacts(ChatMessageDto message)
    {
        // 优先使用已有的 InteractiveBlocks
        if (message.InteractiveBlocks != null && message.InteractiveBlocks.Any())
        {
            return message.InteractiveBlocks;
        }
        
        // 如果 OrderedBlocks 中已有 InteractiveCard，则不从 Artifacts 重复提取
        if (message.OrderedBlocks != null && message.OrderedBlocks.Any(b => b.BlockType == BlockType.InteractiveCard))
        {
            return new List<BlockDto>();
        }
        
        // 从 Artifacts 中提取 interactive-* 类型
        if (message.Artifacts == null || !message.Artifacts.Any())
        {
            return new List<BlockDto>();
        }
        
        // 使用 HashSet 去重
        var seenContents = new HashSet<string>();
        var results = new List<BlockDto>();
        
        foreach (var a in message.Artifacts.Where(a => a.Type?.StartsWith("interactive-", StringComparison.OrdinalIgnoreCase) == true))
        {
            var contentKey = a.Content ?? "";
            if (seenContents.Contains(contentKey))
            {
                continue; // 跳过重复内容
            }
            seenContents.Add(contentKey);
            
            var cardType = a.Type?.Replace("interactive-", "") ?? "unknown";
            
            results.Add(new BlockDto
            {
                BlockType = BlockType.InteractiveCard,
                Content = a.Content!,
                MessageId = a.MessageId,
                Metadata = new Dictionary<string, object>
                {
                    { ToolBlockMetadataConstants.CardType, ToolBlockMetadataConstants.NormalizeCardType(cardType) },
                    { ToolBlockMetadataConstants.Query, a.Name?.Replace("搜索结果", "").Trim() ?? string.Empty }
                }
            });
        }
        
        return results;
    }

    /// <summary>
    /// 获取需要在 Artifacts 卡片区域显示的 Artifact 列表
    /// 过滤掉已经通过 OrderedBlocks 渲染的 Artifact 类型
    /// </summary>
    public static List<ArtifactDto> GetFilteredArtifactsForDisplay(ChatMessageDto message)
    {
        if (message.Artifacts == null || !message.Artifacts.Any())
        {
            return new List<ArtifactDto>();
        }
        
        // 如果没有 OrderedBlocks，则显示所有 Artifacts
        if (message.OrderedBlocks == null || !message.OrderedBlocks.Any())
        {
            return message.Artifacts.ToList();
        }
        
        // 检查 OrderedBlocks 中已包含的类型
        var hasChartBlocks = message.OrderedBlocks.Any(b => b.BlockType == BlockType.Chart);
        var hasInteractiveBlocks = message.OrderedBlocks.Any(b => b.BlockType == BlockType.InteractiveCard);
        var hasArtifactBlocks = message.OrderedBlocks.Any(b => 
            b.BlockType == BlockType.ArtifactStart || 
            b.BlockType == BlockType.ArtifactDelta || 
            b.BlockType == BlockType.ArtifactEnd);
        
        return message.Artifacts
            .Where(a => {
                var type = a.Type?.ToLower() ?? "";
                
                if (hasChartBlocks && type == "chart")
                    return false;
                
                if (hasInteractiveBlocks && type.StartsWith("interactive-"))
                    return false;
                
                if (hasArtifactBlocks && (type == "code" || type == "html" || type == "diff" || 
                    type == "mermaid" || type == "sql" || type == "json" || type == "markdown" || type == "md"))
                    return false;
                
                return true;
            })
            .ToList();
    }
}
