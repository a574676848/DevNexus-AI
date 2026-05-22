using DevNexus.Core.Services.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// Prompt 动态上下文消息构建器测试。
/// </summary>
public sealed class PromptDynamicContextMessageBuilderTests
{
    /// <summary>
    /// 空白动态上下文不应生成注入消息。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnNull_WhenDynamicContextIsBlank()
    {
        PromptDynamicContextMessageBuilder.Build("  ").Should().BeNull();
    }

    /// <summary>
    /// 动态上下文应带系统注入前缀。
    /// </summary>
    [Fact]
    public void Build_ShouldCreateSystemInjectedMessage()
    {
        var message = PromptDynamicContextMessageBuilder.Build("当前工具面板：Direct");

        message.Should().NotBeNull();
        message.Should().StartWith(PromptDynamicContextMessageBuilder.SystemInjectedPrefix);
        PromptDynamicContextMessageBuilder.IsSystemInjected(message).Should().BeTrue();
    }

    /// <summary>
    /// 带标题的动态上下文应保留标题。
    /// </summary>
    [Fact]
    public void Build_ShouldKeepTitle_WhenTitleIsProvided()
    {
        var message = PromptDynamicContextMessageBuilder.Build("文档与 RAG 上下文", "片段 A");

        message.Should().Contain("[文档与 RAG 上下文]");
        message.Should().Contain("不应作为缓存标记候选");
    }
}
