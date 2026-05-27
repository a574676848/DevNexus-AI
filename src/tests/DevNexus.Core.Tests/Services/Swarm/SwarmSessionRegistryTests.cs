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
    /// 中止状态不应被恢复操作覆盖，避免取消后又被标记为运行中。
    /// </summary>
    [Fact]
    public void Resume_ShouldNotOverwriteAbortedStatus()
    {
        var registry = new SwarmSessionRegistry();
        registry.RegisterSession("session-1");

        registry.Abort("session-1");
        registry.Resume("session-1");

        registry.GetStatus("session-1").Should().Be(SwarmControlStatus.Aborted);
    }

    /// <summary>
    /// 中止状态不应被暂停操作覆盖，避免终态回退。
    /// </summary>
    [Fact]
    public void Pause_ShouldNotOverwriteAbortedStatus()
    {
        var registry = new SwarmSessionRegistry();
        registry.RegisterSession("session-1");

        registry.Abort("session-1");
        registry.Pause("session-1");

        registry.GetStatus("session-1").Should().Be(SwarmControlStatus.Aborted);
    }

    /// <summary>
    /// 直接写入运行态也不应覆盖已中止的终态。
    /// </summary>
    [Fact]
    public void SetStatus_ShouldNotOverwriteAbortedStatusWithRunning()
    {
        var registry = new SwarmSessionRegistry();
        registry.RegisterSession("session-1");

        registry.Abort("session-1");
        registry.SetStatus("session-1", SwarmControlStatus.Running);

        registry.GetStatus("session-1").Should().Be(SwarmControlStatus.Aborted);
    }

    /// <summary>
    /// 状态快照应携带读取瞬间的暂停态。
    /// </summary>
    [Fact]
    public void GetSnapshot_ShouldReturnPausedSnapshot()
    {
        var registry = new SwarmSessionRegistry();
        registry.RegisterSession("session-1");

        registry.Pause("session-1");
        var snapshot = registry.GetSnapshot("session-1");

        snapshot.SessionId.Should().Be("session-1");
        snapshot.Status.Should().Be(SwarmControlStatus.Paused);
        snapshot.IsPaused.Should().BeTrue();
    }

    /// <summary>
    /// 快照对象不应随后续状态变化而漂移。
    /// </summary>
    [Fact]
    public void GetSnapshot_ShouldKeepOriginalStatus_WhenRegistryChangesLater()
    {
        var registry = new SwarmSessionRegistry();
        registry.RegisterSession("session-1");
        registry.Pause("session-1");

        var snapshot = registry.GetSnapshot("session-1");
        registry.Resume("session-1");

        snapshot.Status.Should().Be(SwarmControlStatus.Paused);
        snapshot.IsPaused.Should().BeTrue();
        registry.GetStatus("session-1").Should().Be(SwarmControlStatus.Running);
    }

    /// <summary>
    /// 未注册会话的快照状态应保持为空。
    /// </summary>
    [Fact]
    public void GetSnapshot_ShouldReturnNullStatus_WhenSessionMissing()
    {
        var registry = new SwarmSessionRegistry();

        var snapshot = registry.GetSnapshot("missing-session");

        snapshot.SessionId.Should().Be("missing-session");
        snapshot.Status.Should().BeNull();
        snapshot.IsPaused.Should().BeFalse();
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
