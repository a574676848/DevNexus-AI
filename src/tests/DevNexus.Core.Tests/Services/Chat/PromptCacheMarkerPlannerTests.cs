using DevNexus.Core.Services.Chat;
using FluentAssertions;
using Microsoft.SemanticKernel.ChatCompletion;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// Prompt 缓存标记规划器测试。
/// </summary>
public sealed class PromptCacheMarkerPlannerTests
{
    /// <summary>
    /// 标记候选应跳过系统消息并选择最近两个真实对话消息。
    /// </summary>
    [Fact]
    public void Plan_ShouldSelectLatestTwoConversationMessages()
    {
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage("稳定系统提示");
        chatHistory.AddUserMessage("第一轮用户消息");
        chatHistory.AddAssistantMessage("第一轮助手回复");
        chatHistory.AddSystemMessage("[对话历史摘要] 动态摘要");
        chatHistory.AddUserMessage(PromptDynamicContextMessageBuilder.Build("当前动态上下文")!);
        chatHistory.AddUserMessage(PromptDynamicContextMessageBuilder.Build("文档与 RAG 上下文", "检索片段")!);
        chatHistory.AddUserMessage("当前用户消息");

        var plan = PromptCacheMarkerPlanner.Plan(chatHistory);

        plan.MarkerIndexes.Should().Equal(2, 6);
        plan.IsDoubleMarkerReady.Should().BeTrue();
        plan.ReadinessReason.Should().Be(PromptCacheMarkerPlan.ReadyReason);
    }

    /// <summary>
    /// 标记候选应跳过工具运行态消息，只保留真实用户与助手消息。
    /// </summary>
    [Fact]
    public void Plan_ShouldSkipToolRuntimeMessages()
    {
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage("稳定系统提示");
        chatHistory.AddUserMessage("第一轮用户消息");
        chatHistory.AddMessage(AuthorRole.Tool, "工具执行输出");
        chatHistory.AddAssistantMessage("第一轮助手回复");
        chatHistory.AddMessage(new AuthorRole("developer"), "开发者运行态消息");
        chatHistory.AddUserMessage("当前用户消息");

        var plan = PromptCacheMarkerPlanner.Plan(chatHistory);

        plan.MarkerIndexes.Should().Equal(3, 5);
        plan.IsDoubleMarkerReady.Should().BeTrue();
        plan.ReadinessReason.Should().Be(PromptCacheMarkerPlan.ReadyReason);
    }

    /// <summary>
    /// 少于两个真实对话消息时不应标记为双标记就绪。
    /// </summary>
    [Fact]
    public void Plan_ShouldNotBeReady_WhenConversationMessagesAreInsufficient()
    {
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage("稳定系统提示");
        chatHistory.AddUserMessage("当前用户消息");

        var plan = PromptCacheMarkerPlanner.Plan(chatHistory);

        plan.MarkerIndexes.Should().ContainSingle().Which.Should().Be(1);
        plan.IsDoubleMarkerReady.Should().BeFalse();
        plan.ReadinessReason.Should().Be(PromptCacheMarkerPlan.InsufficientConversationMessagesReason);
    }
}
