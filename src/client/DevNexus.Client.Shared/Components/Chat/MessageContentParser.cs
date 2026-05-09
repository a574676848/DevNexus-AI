using System.Text.RegularExpressions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Client.Shared.Components.Chat;

/// <summary>
/// 消息内容解析器，负责解析思维链、过滤块标记等
/// </summary>
public static class MessageContentParser
{
    /// <summary>
    /// 解析消息内容，提取思维链和显示内容
    /// </summary>
    public static (string DisplayedContent, List<string> Thoughts) ParseContent(string content, string senderType)
    {
        if (string.IsNullOrEmpty(content))
        {
            return ("", new List<string>());
        }

        // 用户消息直接返回
        if (ChatConstants.IsUserSender(senderType))
        {
            return (content, new List<string>());
        }

        var thoughts = new List<string>();
        var displayedContent = content;

        // 1. 移除所有 <think> 或 <thought> 标签的内容
        var thinkMatches = Regex.Matches(content, @"<(?:think|thought)>(.*?)</(?:think|thought)>", 
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        if (thinkMatches.Count > 0)
        {
            foreach (Match match in thinkMatches)
            {
                var rawThought = match.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(rawThought))
                {
                    // 统一按换行符拆分步骤，与流式输出保持一致
                    var steps = rawThought.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => s.Trim())
                                        .Where(s => !string.IsNullOrWhiteSpace(s))
                                        .ToArray();
                    
                    if (steps.Length > 0)
                    {
                        thoughts.AddRange(steps);
                    }
                    else
                    {
                        thoughts.Add(rawThought);
                    }
                }
                
                // 从显示内容中移除思考过程
                displayedContent = displayedContent.Replace(match.Value, "").Trim();
            }
        }
        
        // 2. 移除所有 :::block{...} 标记（这些已通过 OrderedBlocks 渲染）
        displayedContent = Regex.Replace(displayedContent, 
            @":::(\w+(?:-\w+)*)(?:\{[^}]*\})?\s*[\r\n]*[\s\S]*?[\r\n]*:::", 
            "", 
            RegexOptions.Multiline);
        
        // 清理多余的空行
        displayedContent = Regex.Replace(displayedContent, @"\n{3,}", "\n\n").Trim();
        
        return (displayedContent, thoughts);
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
