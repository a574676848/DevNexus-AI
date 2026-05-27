using DevNexus.Core.Services.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

public sealed class ChatGenerationCancellationRegistryTests
{
    [Fact]
    public void TryRegister_ShouldRejectSecondGenerationForSameSession()
    {
        var registry = new ChatGenerationCancellationRegistry();
        var sessionId = Guid.NewGuid();
        using var first = new CancellationTokenSource();
        using var second = new CancellationTokenSource();

        registry.TryRegister(sessionId, first).Should().BeTrue();
        registry.TryRegister(sessionId, second).Should().BeFalse();

        registry.Count.Should().Be(1);
        first.IsCancellationRequested.Should().BeFalse();
        second.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void Cancel_ShouldCancelAndRemoveRegisteredSource()
    {
        var registry = new ChatGenerationCancellationRegistry();
        var sessionId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        registry.TryRegister(sessionId, cts).Should().BeTrue();

        registry.Cancel(sessionId).Should().BeTrue();

        cts.IsCancellationRequested.Should().BeTrue();
        registry.Count.Should().Be(0);
        registry.Cancel(sessionId).Should().BeFalse();
    }

    [Fact]
    public void Complete_ShouldOnlyRemoveMatchingSource()
    {
        var registry = new ChatGenerationCancellationRegistry();
        var sessionId = Guid.NewGuid();
        using var registered = new CancellationTokenSource();
        using var other = new CancellationTokenSource();
        registry.TryRegister(sessionId, registered).Should().BeTrue();

        registry.Complete(sessionId, other).Should().BeFalse();

        registry.Count.Should().Be(1);
        registry.Complete(sessionId, registered).Should().BeTrue();
        registry.Count.Should().Be(0);
    }

    [Fact]
    public void Complete_ShouldNotRemoveNewSourceAfterCancelledSourceWasReplaced()
    {
        var registry = new ChatGenerationCancellationRegistry();
        var sessionId = Guid.NewGuid();
        using var cancelled = new CancellationTokenSource();
        using var replacement = new CancellationTokenSource();
        registry.TryRegister(sessionId, cancelled).Should().BeTrue();
        registry.Cancel(sessionId).Should().BeTrue();
        registry.TryRegister(sessionId, replacement).Should().BeTrue();

        registry.Complete(sessionId, cancelled).Should().BeFalse();

        registry.Count.Should().Be(1);
        replacement.IsCancellationRequested.Should().BeFalse();
        registry.Cancel(sessionId).Should().BeTrue();
        replacement.IsCancellationRequested.Should().BeTrue();
    }
}
