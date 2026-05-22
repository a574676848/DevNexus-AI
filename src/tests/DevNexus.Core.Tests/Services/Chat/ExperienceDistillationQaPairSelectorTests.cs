using DevNexus.Core.Services.Chat;
using DevNexus.Shared.Constants;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 经验提纯问答对选择器测试。
/// </summary>
public sealed class ExperienceDistillationQaPairSelectorTests
{
    /// <summary>
    /// 多轮会话应选择最近相邻的用户-助手问答对。
    /// </summary>
    [Fact]
    public void SelectLatestCompletedPair_ShouldSelectLatestAdjacentQaPair()
    {
        var pair = ExperienceDistillationQaPairSelector.SelectLatestCompletedPair(
        [
            User("第一轮问题"),
            Assistant("第一轮回答"),
            User("第二轮问题"),
            Assistant("第二轮回答")
        ]);

        pair.Should().NotBeNull();
        pair!.Question.Should().Be("第二轮问题");
        pair.Answer.Should().Be("第二轮回答");
    }

    /// <summary>
    /// 空文本问答对应跳过。
    /// </summary>
    [Fact]
    public void SelectLatestCompletedPair_ShouldSkipBlankText()
    {
        var pair = ExperienceDistillationQaPairSelector.SelectLatestCompletedPair(
        [
            User("有效问题"),
            Assistant("有效回答"),
            User(" "),
            Assistant("最新回答")
        ]);

        pair.Should().NotBeNull();
        pair!.Question.Should().Be("有效问题");
        pair.Answer.Should().Be("有效回答");
    }

    /// <summary>
    /// 没有相邻用户-助手问答对时应返回空。
    /// </summary>
    [Fact]
    public void SelectLatestCompletedPair_ShouldReturnNull_WhenNoAdjacentPairExists()
    {
        var pair = ExperienceDistillationQaPairSelector.SelectLatestCompletedPair(
        [
            Assistant("孤立回答"),
            User("孤立问题")
        ]);

        pair.Should().BeNull();
    }

    private static ExperienceDistillationQaMessage User(string text)
    {
        return new ExperienceDistillationQaMessage
        {
            SenderType = ChatConstants.RoleUser,
            Text = text
        };
    }

    private static ExperienceDistillationQaMessage Assistant(string text)
    {
        return new ExperienceDistillationQaMessage
        {
            SenderType = ChatConstants.RoleAssistant,
            Text = text
        };
    }
}
