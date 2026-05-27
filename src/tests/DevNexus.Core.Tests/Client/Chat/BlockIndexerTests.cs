using DevNexus.Client.Shared.Services.Chat;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Client.Chat;

public sealed class BlockIndexerTests
{
    [Fact]
    public void AddBlock_ShouldMergeArtifactStreamIntoSingleOrderedBlock()
    {
        var messageId = Guid.NewGuid();
        var indexer = new BlockIndexer();

        indexer.AddBlock(CreateArtifactBlock(BlockType.ArtifactStart, messageId, "file-1", "public "));
        indexer.AddBlock(CreateArtifactBlock(BlockType.ArtifactDelta, messageId, "file-1", "class "));
        indexer.AddBlock(CreateArtifactBlock(BlockType.ArtifactDelta, messageId, "file-1", "Demo"));
        indexer.AddBlock(CreateArtifactBlock(BlockType.ArtifactEnd, messageId, "file-1", string.Empty));

        var orderedBlocks = indexer.GetOrderedBlocks();

        orderedBlocks.Should().ContainSingle();
        orderedBlocks[0].BlockType.Should().Be(BlockType.ArtifactStart);
        orderedBlocks[0].Content.Should().Be("public class Demo");
        orderedBlocks[0].IsLast.Should().BeTrue();
    }

    [Fact]
    public void AddBlock_ShouldPreserveArtifactPositionBetweenTextBlocks()
    {
        var messageId = Guid.NewGuid();
        var indexer = new BlockIndexer();

        indexer.AddBlock(CreateTextBlock(messageId, "前置说明"));
        indexer.AddBlock(CreateArtifactBlock(BlockType.ArtifactStart, messageId, "file-1", "code"));
        indexer.AddBlock(CreateTextBlock(messageId, "后续说明"));

        var orderedBlocks = indexer.GetOrderedBlocks();

        orderedBlocks.Select(block => block.BlockType).Should().Equal(
            BlockType.TextDelta,
            BlockType.ArtifactStart,
            BlockType.TextDelta);
    }

    [Fact]
    public void AddBlock_ShouldNotMergeDistinctCompletedArtifactsInSameMessage()
    {
        var messageId = Guid.NewGuid();
        var indexer = new BlockIndexer();

        indexer.AddBlock(CreateCompletedArtifactBlock(messageId, "one"));
        indexer.AddBlock(CreateCompletedArtifactBlock(messageId, "two"));

        var orderedBlocks = indexer.GetOrderedBlocks();

        orderedBlocks.Should().HaveCount(2);
        orderedBlocks.Select(block => block.Content).Should().Equal("one", "two");
    }

    private static BlockDto CreateTextBlock(Guid messageId, string content)
    {
        return new BlockDto
        {
            BlockId = Guid.NewGuid(),
            MessageId = messageId,
            BlockType = BlockType.TextDelta,
            Content = content
        };
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

    private static BlockDto CreateCompletedArtifactBlock(Guid messageId, string content)
    {
        return new BlockDto
        {
            BlockId = Guid.NewGuid(),
            MessageId = messageId,
            BlockType = BlockType.ArtifactStart,
            Content = content,
            Metadata = new Dictionary<string, object>
            {
                [ArtifactBlockMetadataConstants.ArtifactId] = Guid.NewGuid(),
                [ArtifactBlockMetadataConstants.Type] = ArtifactBlockMetadataConstants.TypeCode,
                [ArtifactBlockMetadataConstants.IsComplete] = true
            }
        };
    }
}
