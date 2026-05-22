using DevNexus.Core.Services.Swarm;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Swarm;

/// <summary>
/// Swarm 会话注册表测试。
/// </summary>
public sealed class SwarmSessionRegistryTests
{
    /// <summary>
    /// 中止会话时应清理该会话下的重试占位。
    /// </summary>
    [Fact]
    public void Abort_ShouldClearActivePackageRetrySlots()
    {
        var registry = new SwarmSessionRegistry();
        registry.RegisterSession("session-1");
        registry.TryBeginPackageRetry("session-1", "package-1").Should().BeTrue();

        registry.Abort("session-1");

        registry.TryBeginPackageRetry("session-1", "package-1").Should().BeTrue();
        registry.GetStatus("session-1").Should().Be(SwarmControlStatus.Aborted);
    }

    /// <summary>
    /// 注销会话时应清理该会话下的重试占位。
    /// </summary>
    [Fact]
    public void UnregisterSession_ShouldClearActivePackageRetrySlots()
    {
        var registry = new SwarmSessionRegistry();
        registry.RegisterSession("session-1");
        registry.TryBeginPackageRetry("session-1", "package-1").Should().BeTrue();

        registry.UnregisterSession("session-1");

        registry.TryBeginPackageRetry("session-1", "package-1").Should().BeTrue();
        registry.GetStatus("session-1").Should().BeNull();
    }
}
