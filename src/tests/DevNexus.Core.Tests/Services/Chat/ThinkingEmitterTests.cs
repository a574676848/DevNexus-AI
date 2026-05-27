using System.Threading.Channels;
using DevNexus.Core.Services.Chat;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

public sealed class ThinkingEmitterTests
{
    [Fact]
    public async Task EmitAsync_ShouldAttachToolInvocationStatusMetadata()
    {
        var channel = Channel.CreateUnbounded<BlockDto>();
        var sessionId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var emitter = new ThinkingEmitter(channel.Writer, sessionId, messageId, CancellationToken.None);

        await emitter.EmitAsync(
            "正在执行工具",
            FeedbackBlockMetadataConstants.SourceToolInvocation,
            ToolInvocationStatus.Running);

        var block = await channel.Reader.ReadAsync();

        block.BlockType.Should().Be(BlockType.Thinking);
        block.SessionId.Should().Be(sessionId);
        block.MessageId.Should().Be(messageId);
        block.Metadata.Should().ContainKey(FeedbackBlockMetadataConstants.Source)
            .WhoseValue.Should().Be(FeedbackBlockMetadataConstants.SourceToolInvocation);
        block.Metadata.Should().ContainKey(FeedbackBlockMetadataConstants.ToolStatus)
            .WhoseValue.Should().Be(ToolInvocationStatus.Running.ToWireValue());
    }
}
