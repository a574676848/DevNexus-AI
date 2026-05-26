using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using DevNexus.Shared.Constants;
using DevNexus.Client.Shared.Abstractions;
using DevNexus.Client.Shared.Helpers;

namespace DevNexus.Client.Shared.Services.Chat;

public class ChatMessageProcessor : IChatMessageProcessor
{
    private readonly ChatBlockCollectionService _blockCollectionService;
    private readonly ChatArtifactTracker _artifactTracker;
    private readonly ChatArtifactPersistenceService _artifactPersistenceService;

    public ChatMessageProcessor(
        ChatBlockCollectionService blockCollectionService,
        ChatArtifactTracker artifactTracker,
        ChatArtifactPersistenceService artifactPersistenceService)
    {
        _blockCollectionService = blockCollectionService;
        _artifactTracker = artifactTracker;
        _artifactPersistenceService = artifactPersistenceService;
    }

    public void ProcessBlock(BlockDto block, List<BlockDto> currentBlocks, ref ArtifactDto? currentArtifact, List<ArtifactDto> completedArtifacts)
    {
        switch (block.BlockType)
        {
            case BlockType.TextDelta:
            case BlockType.Thinking:
                _blockCollectionService.AddBlockIfMissing(block, currentBlocks);
                break;

            case BlockType.InteractiveCard:
                _blockCollectionService.ProcessInteractiveCard(block, currentBlocks, completedArtifacts);
                break;

            case BlockType.Chart:
            case BlockType.Warning:
            case BlockType.Reference:
            case BlockType.Truncated:
                _blockCollectionService.AddBlockIfMissing(block, currentBlocks);
                break;
            case BlockType.Terminal:
                _blockCollectionService.ProcessTerminalBlock(block, currentBlocks);
                break;

            case BlockType.ArtifactStart:
                _artifactTracker.ProcessArtifactStart(block, ref currentArtifact);
                break;

            case BlockType.ArtifactDelta:
                _artifactTracker.ProcessArtifactDelta(block, ref currentArtifact);
                break;

            case BlockType.ArtifactEnd:
                _artifactTracker.ProcessArtifactEnd(block, ref currentArtifact, completedArtifacts);
                break;
        }
    }

    public async Task<ChatMessageDto?> BuildChatMessageAsync(
        Guid sessionId, 
        Guid messageId, 
        List<BlockDto> blocks, 
        List<ArtifactDto> completedArtifacts,
        ArtifactDto? currentArtifact)
    {
        if (!blocks.Any()) return null;

        var textContent = string.Join("", blocks.Where(b => b.BlockType == BlockType.TextDelta).Select(b => b.Content));
        
        // 获取思维链内容
        var thoughtContent = MetadataHelper.JoinThoughtSegments(blocks
            .Where(b => b.BlockType == BlockType.Thinking)
            .Select(b => b.Content));
        
        // 提取图表 Blocks
        var chartBlocks = blocks.Where(b => b.BlockType == BlockType.Chart).ToList();
        
        // 提取交互卡片 Blocks
        var interactiveBlocks = blocks.Where(b => b.BlockType == BlockType.InteractiveCard).ToList();
        
        // 提取警告 Blocks
        var warningBlocks = blocks.Where(b => b.BlockType == BlockType.Warning).ToList();

        // 提取所有非文本 Block 的原始顺序
        var orderedBlocks = blocks
            .Where(b => b.BlockType != BlockType.TextDelta
                     && b.BlockType != BlockType.Thinking)
            .ToList();

        // 构建完整内容
        var aiContent = string.IsNullOrEmpty(thoughtContent) 
                ? textContent 
                : $"<think>{thoughtContent}</think>\n{textContent}";
        
        if (string.IsNullOrEmpty(aiContent) && !chartBlocks.Any() && !interactiveBlocks.Any() && !warningBlocks.Any() && !orderedBlocks.Any())
        {
            return null;
        }

        var aiMessage = new ChatMessageDto
        {
            Id = messageId,
            ChatSessionId = sessionId,
            SenderType = ChatConstants.RoleAssistant,
            Content = aiContent,
            CreatedAt = DateTime.UtcNow,
            ChartBlocks = chartBlocks.Any() ? chartBlocks : null,
            InteractiveBlocks = interactiveBlocks.Any() ? interactiveBlocks : null,
            OrderedBlocks = orderedBlocks.Any() ? orderedBlocks : null
        };
        
        // 关联已完成的 Artifacts 到消息
        if (completedArtifacts.Any())
        {
            aiMessage.Artifacts = completedArtifacts.ToList();
        }

        // 保存所有 Artifacts 到数据库 (异步)
        await _artifactPersistenceService.SaveArtifactsToDbAsync(
            sessionId,
            messageId,
            completedArtifacts,
            chartBlocks,
            interactiveBlocks,
            currentArtifact);

        return aiMessage;
    }
}

