using DevNexus.Core.Services.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// Prompt 缓存键构建器测试。
/// </summary>
public sealed class PromptCacheKeyBuilderTests
{
    /// <summary>
    /// 相同稳定前缀和工具 Schema 应生成相同缓存键。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnSameKey_WhenStableInputsAreSame()
    {
        var first = PromptCacheKeyBuilder.Build("ABC", "TOOLS");
        var second = PromptCacheKeyBuilder.Build("abc", "tools");

        first.Should().Be(second);
        first.Should().HaveLength(64);
    }

    /// <summary>
    /// 工具 Schema 变化应改变缓存键。
    /// </summary>
    [Fact]
    public void Build_ShouldChangeKey_WhenToolSchemaHashChanges()
    {
        var first = PromptCacheKeyBuilder.Build("stable-prefix", "tools-a");
        var second = PromptCacheKeyBuilder.Build("stable-prefix", "tools-b");

        first.Should().NotBe(second);
    }

    /// <summary>
    /// 空工具 Schema 应使用固定占位，避免空白漂移。
    /// </summary>
    [Fact]
    public void Build_ShouldNormalizeBlankToolSchemaHash()
    {
        var first = PromptCacheKeyBuilder.Build("stable-prefix", null);
        var second = PromptCacheKeyBuilder.Build("stable-prefix", " ");

        first.Should().Be(second);
    }
}
