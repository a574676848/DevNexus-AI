using DevNexus.Client.Shared.Services.Chat;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Client.Shared.Components.Chat;

public partial class ChatContainer
{
    private readonly Dictionary<string, ArtifactDto> _streamingArtifactsBySemanticId = new(StringComparer.Ordinal);

    private void ApplyStreamingBlockState(BlockDto block)
    {
        var targetSessionId = block.SessionId != Guid.Empty
            ? block.SessionId
            : ChatState.CurrentSessionId;

        ApplySwarmBlockState(block, targetSessionId);
        ChatState.AddBlock(targetSessionId, block);

        if (targetSessionId != ChatState.CurrentSessionId)
        {
            return;
        }

        ApplyStreamingArtifactState(block);
    }

    private void ApplySwarmBlockState(BlockDto block, Guid targetSessionId)
    {
        if (block.Metadata == null || !block.Metadata.TryGetValue("swarmEvent", out var swarmEventObj))
        {
            return;
        }

        var swarmEvent = SwarmEventNames.Normalize(swarmEventObj?.ToString());
        if (SwarmEventNames.IsStarted(swarmEvent))
        {
            ChatState.SetSwarmActive(targetSessionId, true);
        }
        else if (SwarmEventNames.IsTerminal(swarmEvent))
        {
            ChatState.SetSwarmActive(targetSessionId, false);
        }
    }

    private void ApplyStreamingArtifactState(BlockDto block)
    {
        switch (block.BlockType)
        {
            case BlockType.ArtifactStart:
                StartStreamingArtifact(block);
                break;
            case BlockType.ArtifactDelta:
                AppendStreamingArtifact(block);
                break;
            case BlockType.ArtifactEnd:
                CompleteStreamingArtifact(block);
                break;
            case BlockType.InteractiveCard:
                AddInteractiveArtifact(block);
                break;
        }
    }

    private void StartStreamingArtifact(BlockDto block)
    {
        if (block.Action == BlockAction.Delete)
        {
            if (!string.IsNullOrWhiteSpace(block.ArtifactId))
            {
                _streamingArtifactsBySemanticId.Remove(block.ArtifactId);
            }

            _currentArtifact = null;
            return;
        }

        _currentArtifact = CreateStreamingArtifact(block);
        if (!string.IsNullOrWhiteSpace(block.ArtifactId))
        {
            _streamingArtifactsBySemanticId[block.ArtifactId] = _currentArtifact;
        }

        ChatState.SetArtifact(block.SessionId, _currentArtifact);
    }

    private void AppendStreamingArtifact(BlockDto block)
    {
        if (_currentArtifact == null)
        {
            _currentArtifact = CreateStreamingArtifact(block);
        }

        _currentArtifact.Content += block.Content;
        ChatState.SetArtifact(block.SessionId, _currentArtifact);
    }

    private void CompleteStreamingArtifact(BlockDto block)
    {
        if (_currentArtifact == null)
        {
            _currentArtifact = CreateStreamingArtifact(block);
        }

        if (!string.IsNullOrEmpty(block.Content))
        {
            _currentArtifact.Content += block.Content;
        }

        ChatState.SetArtifact(block.SessionId, _currentArtifact);
        if (_completedArtifacts.All(artifact => artifact.ArtifactId != _currentArtifact.ArtifactId))
        {
            _completedArtifacts.Add(_currentArtifact);
        }

        if (!string.IsNullOrWhiteSpace(_currentArtifact.SemanticId))
        {
            _streamingArtifactsBySemanticId[_currentArtifact.SemanticId] = _currentArtifact;
        }

        _currentArtifact = null;
    }

    private void AddInteractiveArtifact(BlockDto block)
    {
        var cardType = ToolBlockMetadataConstants.NormalizeCardType(
            ChatMessageMetadataReader.GetString(block.Metadata, ToolBlockMetadataConstants.CardType));
        if (!ToolBlockMetadataConstants.IsSearchLikeCardType(cardType))
        {
            return;
        }

        var status = ToolBlockMetadataConstants.NormalizeStatus(
            ChatMessageMetadataReader.GetString(block.Metadata, ToolBlockMetadataConstants.Status),
            string.Empty);
        if (ToolBlockMetadataConstants.IsLoadingStatus(status) || string.IsNullOrWhiteSpace(block.Content))
        {
            return;
        }

        var identity = cardType == ToolBlockMetadataConstants.CardTypeWebpage
            ? ChatMessageMetadataReader.GetString(block.Metadata, ToolBlockMetadataConstants.Url) ?? "网页阅读"
            : ChatMessageMetadataReader.GetString(block.Metadata, ToolBlockMetadataConstants.Query) ?? "搜索结果";
        var artifactType = $"interactive-{cardType}";

        if (_completedArtifacts.Any(artifact =>
                artifact.Type?.Equals(artifactType, StringComparison.OrdinalIgnoreCase) == true &&
                artifact.Content == block.Content &&
                artifact.MessageId == block.MessageId))
        {
            return;
        }

        _completedArtifacts.Add(new ArtifactDto
        {
            ArtifactId = Guid.NewGuid(),
            Type = artifactType,
            Name = cardType switch
            {
                ToolBlockMetadataConstants.CardTypeAdvancedSearch => $"高级搜索 {identity}",
                ToolBlockMetadataConstants.CardTypeWebpage => $"网页阅读 {identity}",
                _ => $"搜索结果 {identity}"
            },
            Content = block.Content,
            SessionId = block.SessionId,
            MessageId = block.MessageId
        });
    }

    private ArtifactDto CreateStreamingArtifact(BlockDto block)
    {
        var semanticId = block.ArtifactId;
        var parent = !string.IsNullOrWhiteSpace(semanticId) &&
            _streamingArtifactsBySemanticId.TryGetValue(semanticId, out var existing)
            ? existing
            : null;

        return new ArtifactDto
        {
            ArtifactId = ChatMessageMetadataReader.GetGuid(block.Metadata, ArtifactBlockMetadataConstants.ArtifactId)
                ?? Guid.NewGuid(),
            SemanticId = semanticId,
            Version = block.Version,
            BaseVersion = parent?.Version,
            ParentArtifactId = parent?.ArtifactId,
            Type = ChatMessageMetadataReader.GetString(block.Metadata, ArtifactBlockMetadataConstants.Language)
                ?? ChatMessageMetadataReader.GetString(block.Metadata, ArtifactBlockMetadataConstants.Type)
                ?? ArtifactBlockMetadataConstants.TypeCode,
            Name = ChatMessageMetadataReader.GetString(block.Metadata, ArtifactBlockMetadataConstants.Title)
                ?? parent?.Name
                ?? ArtifactBlockMetadataConstants.DefaultArtifactTitle,
            Content = block.Content ?? string.Empty,
            SessionId = block.SessionId,
            MessageId = block.MessageId
        };
    }
}
