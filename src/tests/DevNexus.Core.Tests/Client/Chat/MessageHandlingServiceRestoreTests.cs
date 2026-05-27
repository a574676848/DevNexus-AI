using DevNexus.Client.Shared.Services.Chat;
using DevNexus.Client.Shared.Services.State;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevNexus.Core.Tests.Client.Chat;

public sealed class MessageHandlingServiceRestoreTests
{
    [Fact]
    public void RestoreGeneratingState_ShouldRebuildMergedBlocksFromRawChatStateBlocks()
    {
        var sessionId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var chatState = new ChatState(NullLogger<ChatState>.Instance);
        chatState.SetCurrentSession(sessionId);
        chatState.AddBlock(sessionId, CreateArtifactBlock(BlockType.ArtifactStart, messageId, "file-1", "public "));
        chatState.AddBlock(sessionId, CreateArtifactBlock(BlockType.ArtifactDelta, messageId, "file-1", "class Demo"));
        chatState.AddBlock(sessionId, CreateArtifactBlock(BlockType.ArtifactEnd, messageId, "file-1", string.Empty));
        var service = new MessageHandlingService(
            apiService: null!,
            chatState,
            sessionState: null!,
            messageProcessor: null!,
            NullLogger<MessageHandlingService>.Instance);
        var currentBlocks = new List<BlockDto>();

        var idsToRemove = service.RestoreGeneratingState(sessionId, currentBlocks);

        idsToRemove.Should().ContainSingle().Which.Should().Be(messageId);
        currentBlocks.Should().ContainSingle();
        currentBlocks[0].BlockType.Should().Be(BlockType.ArtifactStart);
        currentBlocks[0].Content.Should().Be("public class Demo");
        currentBlocks[0].IsLast.Should().BeTrue();
    }

    private static BlockDto CreateArtifactBlock(
        BlockType blockType,
        Guid messageId,
        string artifactId,
        string content)
    {
        return new BlockDto
        {
            BlockId = Guid.NewGuid(),
            MessageId = messageId,
            BlockType = blockType,
            ArtifactId = artifactId,
            Content = content,
            Metadata = new Dictionary<string, object>
            {
                [ArtifactBlockMetadataConstants.Type] = ArtifactBlockMetadataConstants.TypeCode,
                [ArtifactBlockMetadataConstants.Language] = "csharp"
            }
        };
    }
}
