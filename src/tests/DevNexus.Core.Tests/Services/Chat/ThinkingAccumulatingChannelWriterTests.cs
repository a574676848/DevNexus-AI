using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using FluentAssertions;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// Thinking 累积写入器测试。
/// </summary>
public sealed class ThinkingAccumulatingChannelWriterTests
{
    [Fact]
    public async Task WriteAsync_ShouldPersistSnapshotAndClearAccumulator_WhenThresholdReached()
    {
        var innerChannel = Channel.CreateUnbounded<BlockDto>();
        var accumulator = new StringBuilder();
        var writer = new global::ThinkingAccumulatingChannelWriter(innerChannel.Writer, accumulator);
        var persisted = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        writer.SetPersistenceCallback(content =>
        {
            persisted.TrySetResult(content);
            return Task.CompletedTask;
        });

        await writer.WriteAsync(CreateThinkingBlock("一"));
        await writer.WriteAsync(CreateThinkingBlock("二"));
        await writer.WriteAsync(CreateThinkingBlock("三"));

        var completed = await Task.WhenAny(persisted.Task, Task.Delay(TimeSpan.FromSeconds(5)));

        completed.Should().Be(persisted.Task);
        var persistedContent = await persisted.Task;
        persistedContent.Should().Be("一二三");
        accumulator.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task TryWrite_ShouldNotLoseThinkingContent_WhenMultipleWritersReachThreshold()
    {
        const int writeCount = 90;
        var innerChannel = Channel.CreateUnbounded<BlockDto>();
        var accumulator = new StringBuilder();
        var writer = new global::ThinkingAccumulatingChannelWriter(innerChannel.Writer, accumulator);
        var persisted = new ConcurrentBag<string>();
        var allPersisted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        writer.SetPersistenceCallback(content =>
        {
            persisted.Add(content);
            if (persisted.Sum(item => item.Length) >= writeCount)
            {
                allPersisted.TrySetResult();
            }

            return Task.CompletedTask;
        });

        await Task.WhenAll(Enumerable.Range(0, writeCount)
            .Select(_ => Task.Run(() => writer.TryWrite(CreateThinkingBlock("x")).Should().BeTrue())));

        var completed = await Task.WhenAny(allPersisted.Task, Task.Delay(TimeSpan.FromSeconds(5)));

        completed.Should().Be(allPersisted.Task);
        persisted.Sum(item => item.Length).Should().Be(writeCount);
        accumulator.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task SnapshotThinkingContent_ShouldReturnRemainingThinkingWithoutDraining()
    {
        var innerChannel = Channel.CreateUnbounded<BlockDto>();
        var accumulator = new StringBuilder();
        var writer = new global::ThinkingAccumulatingChannelWriter(innerChannel.Writer, accumulator);

        await writer.WriteAsync(CreateThinkingBlock("甲"));
        await writer.WriteAsync(CreateThinkingBlock("乙"));

        writer.SnapshotThinkingContent().Should().Be("甲乙");
        writer.SnapshotThinkingContent().Should().Be("甲乙");
    }

    private static BlockDto CreateThinkingBlock(string content)
    {
        return new BlockDto
        {
            BlockType = BlockType.Thinking,
            Content = content,
            MessageId = Guid.NewGuid(),
            SessionId = Guid.NewGuid()
        };
    }
}
