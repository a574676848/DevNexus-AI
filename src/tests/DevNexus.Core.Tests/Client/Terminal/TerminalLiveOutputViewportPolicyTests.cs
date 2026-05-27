using DevNexus.Client.Shared.Components.Terminal;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Client.Terminal;

public sealed class TerminalLiveOutputViewportPolicyTests
{
    [Fact]
    public void Create_ShouldKeepShortOutput()
    {
        var policy = new TerminalLiveOutputViewportPolicy(maxVisibleChars: 32);

        var viewport = policy.Create("line 1\nline 2");

        viewport.WasTrimmed.Should().BeFalse();
        viewport.Text.Should().Be("line 1\nline 2");
        viewport.VisibleLineCount.Should().Be(2);
    }

    [Fact]
    public void Create_ShouldTrimLongOutputToRecentLineBoundary()
    {
        var policy = new TerminalLiveOutputViewportPolicy(maxVisibleChars: 24);

        var viewport = policy.Create("old line 1\nold line 2\nnew line 3\nnew line 4");

        viewport.WasTrimmed.Should().BeTrue();
        viewport.Text.Should().Be("new line 3\nnew line 4");
        viewport.HiddenLineCount.Should().Be(2);
    }

    [Fact]
    public void Create_ShouldHandleEmptyOutput()
    {
        var viewport = TerminalLiveOutputViewportPolicy.Default.Create(string.Empty);

        viewport.Text.Should().BeEmpty();
        viewport.WasTrimmed.Should().BeFalse();
        viewport.VisibleLineCount.Should().Be(0);
    }
}
