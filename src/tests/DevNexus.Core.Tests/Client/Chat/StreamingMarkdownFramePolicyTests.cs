using DevNexus.Client.Shared.Components.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Client.Chat;

public sealed class StreamingMarkdownFramePolicyTests
{
    [Fact]
    public void BuildNextFrame_ShouldRevealSmallDeltaImmediately()
    {
        var policy = new StreamingMarkdownFramePolicy(largeDeltaThreshold: 8, maxRevealCharsPerFrame: 4);

        var frame = policy.BuildNextFrame("你好", "你好，世界");

        frame.Should().Be("你好，世界");
    }

    [Fact]
    public void BuildNextFrame_ShouldRevealLargeDeltaProgressively()
    {
        var policy = new StreamingMarkdownFramePolicy(largeDeltaThreshold: 8, maxRevealCharsPerFrame: 4);

        var frame = policy.BuildNextFrame("AI", "AI回复消息应该像打字一样持续出现");

        frame.Should().Be("AI回复消息");
    }

    [Fact]
    public void BuildNextFrame_ShouldRenderTargetWhenContentIsRewritten()
    {
        var policy = StreamingMarkdownFramePolicy.Default;

        var frame = policy.BuildNextFrame("旧的回复", "新的回复");

        frame.Should().Be("新的回复");
    }
}
