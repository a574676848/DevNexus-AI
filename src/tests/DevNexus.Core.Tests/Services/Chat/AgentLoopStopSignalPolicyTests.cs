using DevNexus.Core.Services.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// Agent Loop 停止信号策略测试。
/// </summary>
public sealed class AgentLoopStopSignalPolicyTests
{
    /// <summary>
    /// 最后一个非空行是停止标记时应停止自动修复。
    /// </summary>
    [Fact]
    public void ShouldStop_ShouldReturnTrue_WhenLastNonEmptyLineIsStopMarker()
    {
        var response = """
            当前问题缺少必要权限，继续重试没有收益。
            [AGENT_LOOP_STOP]

            """;

        var shouldStop = AgentLoopStopSignalPolicy.ShouldStop(response);

        shouldStop.Should().BeTrue();
    }

    /// <summary>
    /// 正文中引用停止标记时不应误停。
    /// </summary>
    [Fact]
    public void ShouldStop_ShouldReturnFalse_WhenStopMarkerIsOnlyQuoted()
    {
        var response = """
            修复提示要求必要时输出 [AGENT_LOOP_STOP]，但当前还可以继续尝试。
            我会先缩小执行范围。
            """;

        var shouldStop = AgentLoopStopSignalPolicy.ShouldStop(response);

        shouldStop.Should().BeFalse();
    }

    /// <summary>
    /// 停止标记后仍有正文时不应停止自动修复。
    /// </summary>
    [Fact]
    public void ShouldStop_ShouldReturnFalse_WhenTextAppearsAfterStopMarker()
    {
        var response = """
            [AGENT_LOOP_STOP]
            继续补充说明。
            """;

        var shouldStop = AgentLoopStopSignalPolicy.ShouldStop(response);

        shouldStop.Should().BeFalse();
    }
}
