using DevNexus.Client.Shared.Abstractions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Client.Shared.Services.Chat;

public sealed class ChatArtifactTracker
{
    private readonly IChatState _chatState;
    private readonly Dictionary<string, ArtifactDto> _artifactsBySemanticId = new();

    public ChatArtifactTracker(IChatState chatState)
    {
        _chatState = chatState;
    }

    public void ProcessArtifactStart(BlockDto block, ref ArtifactDto? currentArtifact)
    {
        var semanticId = block.ArtifactId;
        var action = block.Action;
        var version = block.Version;

        switch (action)
        {
            case BlockAction.Create:
                currentArtifact = CreateArtifact(block, semanticId, version, Guid.NewGuid());
                _chatState.SetArtifact(block.SessionId, currentArtifact);

                if (!string.IsNullOrEmpty(semanticId))
                {
                    _artifactsBySemanticId[semanticId] = currentArtifact;
                }
                break;

            case BlockAction.Update:
                ProcessArtifactUpdate(block, semanticId, version, ref currentArtifact);
                break;

            case BlockAction.Delete:
                if (!string.IsNullOrEmpty(semanticId))
                {
                    _artifactsBySemanticId.Remove(semanticId);
                }
                break;
        }
    }

    public void ProcessArtifactDelta(BlockDto block, ref ArtifactDto? currentArtifact)
    {
        if (currentArtifact == null)
        {
            return;
        }

        currentArtifact.Content += block.Content;
        _chatState.SetArtifact(block.SessionId, currentArtifact);
    }

    public void ProcessArtifactEnd(
        BlockDto block,
        ref ArtifactDto? currentArtifact,
        List<ArtifactDto> completedArtifacts)
    {
        if (currentArtifact == null)
        {
            return;
        }

        _chatState.SetArtifact(block.SessionId, currentArtifact);
        completedArtifacts.Add(currentArtifact);

        if (!string.IsNullOrEmpty(currentArtifact.SemanticId))
        {
            _artifactsBySemanticId[currentArtifact.SemanticId] = currentArtifact;
        }

        currentArtifact = null;
    }

    public void TryAddInteractiveArtifact(BlockDto block, List<ArtifactDto> completedArtifacts)
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

        var query = ChatMessageMetadataReader.GetString(block.Metadata, ToolBlockMetadataConstants.Query) ?? "搜索结果";
        var url = ChatMessageMetadataReader.GetString(block.Metadata, ToolBlockMetadataConstants.Url) ?? "网页阅读";
        var artifactType = $"interactive-{cardType}";

        if (completedArtifacts.Any(artifact =>
                artifact.Type?.Equals(artifactType, StringComparison.OrdinalIgnoreCase) == true &&
                artifact.Content == block.Content &&
                artifact.MessageId == block.MessageId))
        {
            return;
        }

        completedArtifacts.Add(new ArtifactDto
        {
            ArtifactId = Guid.NewGuid(),
            Type = artifactType,
            Name = cardType switch
            {
                ToolBlockMetadataConstants.CardTypeAdvancedSearch => $"高级搜索 {query}",
                ToolBlockMetadataConstants.CardTypeWebpage => $"网页阅读 {url}",
                _ => $"搜索结果 {query}"
            },
            Content = block.Content ?? string.Empty,
            SessionId = block.SessionId,
            MessageId = block.MessageId
        });
    }

    private void ProcessArtifactUpdate(
        BlockDto block,
        string? semanticId,
        int version,
        ref ArtifactDto? currentArtifact)
    {
        if (!string.IsNullOrEmpty(semanticId)
            && _artifactsBySemanticId.TryGetValue(semanticId, out var existingArtifact))
        {
            var previousArtifactId = existingArtifact.ArtifactId;
            currentArtifact = new ArtifactDto
            {
                ArtifactId = Guid.NewGuid(),
                SemanticId = semanticId,
                Version = version,
                BaseVersion = existingArtifact.Version,
                ParentArtifactId = previousArtifactId,
                Type = existingArtifact.Type,
                Name = ChatMessageMetadataReader.GetString(block.Metadata, ArtifactBlockMetadataConstants.Title) ?? existingArtifact.Name,
                Content = block.Content ?? string.Empty,
                SessionId = block.SessionId,
                MessageId = block.MessageId
            };

            _artifactsBySemanticId[semanticId] = currentArtifact;
            _chatState.SetArtifact(block.SessionId, currentArtifact);
            return;
        }

        currentArtifact = CreateArtifact(block, semanticId, version, Guid.NewGuid());
        _chatState.SetArtifact(block.SessionId, currentArtifact);

        if (!string.IsNullOrEmpty(semanticId))
        {
            _artifactsBySemanticId[semanticId] = currentArtifact;
        }
    }

    private static ArtifactDto CreateArtifact(
        BlockDto block,
        string? semanticId,
        int version,
        Guid fallbackArtifactId)
    {
        return new ArtifactDto
        {
            ArtifactId = ChatMessageMetadataReader.GetGuid(block.Metadata, ArtifactBlockMetadataConstants.ArtifactId) ?? fallbackArtifactId,
            SemanticId = semanticId,
            Version = version,
            Type = ChatMessageMetadataReader.GetString(block.Metadata, ArtifactBlockMetadataConstants.Language)
                ?? ChatMessageMetadataReader.GetString(block.Metadata, ArtifactBlockMetadataConstants.Type)
                ?? ArtifactBlockMetadataConstants.TypeCode,
            Name = ChatMessageMetadataReader.GetString(block.Metadata, ArtifactBlockMetadataConstants.Title) ?? ArtifactBlockMetadataConstants.DefaultArtifactTitle,
            Content = block.Content ?? string.Empty,
            SessionId = block.SessionId,
            MessageId = block.MessageId
        };
    }
}