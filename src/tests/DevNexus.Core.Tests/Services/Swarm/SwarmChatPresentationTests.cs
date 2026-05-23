using DevNexus.Core.Services.Swarm;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Swarm;

/// <summary>
/// Swarm 聊天主线展示文案测试。
/// </summary>
public sealed class SwarmChatPresentationTests
{
    /// <summary>
    /// Swarm 启动提示应保持低噪，不暴露内部评分和诊断字段。
    /// </summary>
    [Fact]
    public void BuildStartedMessage_ShouldHideInternalDiagnostics()
    {
        var message = SwarmChatPresentation.BuildStartedMessage();

        message.Should().Contain("Swarm 协作已启动");
        message.Should().Contain("Swarm 面板");
        message.Should().NotContain("复杂度评分");
        message.Should().NotContain("领域");
        message.Should().NotContain("实时执行拓扑图");
        message.Should().NotContain("🚀");
    }
}
