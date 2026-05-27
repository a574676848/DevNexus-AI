using DevNexus.Client.Shared.Services.Communication;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Client.Communication;

public sealed class PendingGenerationCancelQueueTests
{
    [Fact]
    public void Enqueue_ShouldIgnoreEmptySession()
    {
        var queue = new PendingGenerationCancelQueue();

        queue.Enqueue(Guid.Empty);

        queue.Count.Should().Be(0);
        queue.Drain().Should().BeEmpty();
    }

    [Fact]
    public void Enqueue_ShouldDeduplicateSessionIds()
    {
        var queue = new PendingGenerationCancelQueue();
        var sessionId = Guid.NewGuid();

        queue.Enqueue(sessionId);
        queue.Enqueue(sessionId);

        queue.Count.Should().Be(1);
        queue.Drain().Should().ContainSingle().Which.Should().Be(sessionId);
    }

    [Fact]
    public void Drain_ShouldClearQueuedSessions()
    {
        var queue = new PendingGenerationCancelQueue();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        queue.Enqueue(first);
        queue.Enqueue(second);

        var drained = queue.Drain();

        drained.Should().BeEquivalentTo(new[] { first, second });
        queue.Count.Should().Be(0);
        queue.Drain().Should().BeEmpty();
    }
}
