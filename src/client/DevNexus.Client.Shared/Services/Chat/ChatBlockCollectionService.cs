using DevNexus.Client.Shared.Abstractions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using System.Text.Json;

namespace DevNexus.Client.Shared.Services.Chat;

public sealed class ChatBlockCollectionService
{
    private readonly ChatArtifactTracker _artifactTracker;
    private readonly IRemoteLogService _remoteLog;

    public ChatBlockCollectionService(
        ChatArtifactTracker artifactTracker,
        IRemoteLogService remoteLog)
    {
        _artifactTracker = artifactTracker;
        _remoteLog = remoteLog;
    }

    public void AddBlockIfMissing(BlockDto block, List<BlockDto> currentBlocks)
    {
        if (!currentBlocks.Any(existing => existing.BlockId == block.BlockId))
        {
            currentBlocks.Add(block);
        }
    }

    public void ProcessInteractiveCard(
        BlockDto block,
        List<BlockDto> currentBlocks,
        List<ArtifactDto> completedArtifacts)
    {
        var cardType = ToolBlockMetadataConstants.NormalizeCardType(
            ChatMessageMetadataReader.GetString(block.Metadata, ToolBlockMetadataConstants.CardType));
        var status = ToolBlockMetadataConstants.NormalizeStatus(
            ChatMessageMetadataReader.GetString(block.Metadata, ToolBlockMetadataConstants.Status),
            string.Empty);

        var isSearchLike = ToolBlockMetadataConstants.IsSearchLikeCardType(cardType);
        var identityValue = cardType switch
        {
            ToolBlockMetadataConstants.CardTypeSearch => ChatMessageMetadataReader.GetString(block.Metadata, ToolBlockMetadataConstants.Query),
            ToolBlockMetadataConstants.CardTypeAdvancedSearch => ChatMessageMetadataReader.GetString(block.Metadata, ToolBlockMetadataConstants.Query),
            ToolBlockMetadataConstants.CardTypeWebpage => ChatMessageMetadataReader.GetString(block.Metadata, ToolBlockMetadataConstants.Url),
            _ => null
        };

        if (isSearchLike)
        {
            if (ToolBlockMetadataConstants.IsLoadingStatus(status))
            {
                var existingLoading = currentBlocks.FirstOrDefault(existing =>
                    existing.BlockType == BlockType.InteractiveCard &&
                    ToolBlockMetadataConstants.NormalizeCardType(
                        ChatMessageMetadataReader.GetString(existing.Metadata, ToolBlockMetadataConstants.CardType)) == cardType &&
                    ChatMessageMetadataReader.GetString(existing.Metadata,
                        cardType == ToolBlockMetadataConstants.CardTypeWebpage ? ToolBlockMetadataConstants.Url : ToolBlockMetadataConstants.Query) == identityValue);

                if (existingLoading == null)
                {
                    currentBlocks.Add(block);
                }

                return;
            }

            var loadingIndex = currentBlocks.FindIndex(existing =>
                existing.BlockType == BlockType.InteractiveCard &&
                ToolBlockMetadataConstants.NormalizeCardType(
                    ChatMessageMetadataReader.GetString(existing.Metadata, ToolBlockMetadataConstants.CardType)) == cardType &&
                ChatMessageMetadataReader.GetString(existing.Metadata,
                    cardType == ToolBlockMetadataConstants.CardTypeWebpage ? ToolBlockMetadataConstants.Url : ToolBlockMetadataConstants.Query) == identityValue);

            if (loadingIndex >= 0)
            {
                currentBlocks[loadingIndex] = block;
            }
            else
            {
                currentBlocks.Add(block);
            }

            _artifactTracker.TryAddInteractiveArtifact(block, completedArtifacts);
            return;
        }

        AddBlockIfMissing(block, currentBlocks);
        _artifactTracker.TryAddInteractiveArtifact(block, completedArtifacts);
    }

    public void ProcessTerminalBlock(BlockDto block, List<BlockDto> currentBlocks)
    {
        var terminalStreamId = ChatMessageMetadataReader.GetGuid(block.Metadata, TerminalBlockMetadataKeys.TerminalStreamId);
        if (!terminalStreamId.HasValue)
        {
            AddBlockIfMissing(block, currentBlocks);
            return;
        }

        var existing = currentBlocks.FirstOrDefault(existingBlock =>
            existingBlock.BlockType == BlockType.Terminal &&
            ChatMessageMetadataReader.GetGuid(existingBlock.Metadata, TerminalBlockMetadataKeys.TerminalStreamId) == terminalStreamId.Value);

        if (existing == null)
        {
            currentBlocks.Add(block);
            return;
        }

        if (!string.IsNullOrEmpty(block.Content))
        {
            existing.Content += block.Content;
        }

        MergeMetadata(existing, block.Metadata);

        if (block.IsLast)
        {
            existing.IsLast = true;
        }
    }

    public void ProcessToolResultBlock(BlockDto block, List<BlockDto> currentBlocks)
    {
        try
        {
            var toolCallId = ChatMessageMetadataReader.GetGuid(block.Metadata, ToolBlockMetadataConstants.ToolCallId);
            var existingIndex = FindToolCallIndex(currentBlocks, toolCallId);

            if (block.Metadata == null || !block.Metadata.TryGetValue(ToolBlockMetadataConstants.ToolName, out var toolName))
            {
                UpsertToolResultBlock(currentBlocks, block, existingIndex);
                return;
            }

            UpsertToolResultBlock(currentBlocks, block, existingIndex);
        }
        catch (Exception ex)
        {
            _remoteLog.LogErrorAsync(ex, "ChatMessageProcessor.ProcessToolResultBlock", new Dictionary<string, object?>
            {
                ["content"] = block.Content
            }).GetAwaiter().GetResult();

            currentBlocks.Add(block);
        }
    }

    private static void MergeMetadata(BlockDto target, Dictionary<string, object>? source)
    {
        if (source == null || source.Count == 0)
        {
            return;
        }

        target.Metadata ??= new Dictionary<string, object>();
        foreach (var item in source)
        {
            if (item.Value != null)
            {
                target.Metadata[item.Key] = item.Value;
            }
        }
    }

    private static void UpsertToolResultBlock(List<BlockDto> currentBlocks, BlockDto block, int existingIndex)
    {
        if (existingIndex >= 0)
        {
            currentBlocks[existingIndex] = block;
            return;
        }

        currentBlocks.Add(block);
    }

    private static int FindToolCallIndex(List<BlockDto> currentBlocks, Guid? toolCallId)
    {
        if (!toolCallId.HasValue)
        {
            return -1;
        }

        return currentBlocks.FindIndex(block =>
        {
            var existingId = ChatMessageMetadataReader.GetGuid(block.Metadata, ToolBlockMetadataConstants.ToolCallId);
            return existingId.HasValue && existingId.Value == toolCallId.Value;
        });
    }
}
