using DevNexus.Core.Services.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// Prompt 缓存命名空间构建器测试。
/// </summary>
public sealed class PromptCacheNamespaceBuilderTests
{
    /// <summary>
    /// 相同 Provider 与模型上下文应生成相同命名空间。
    /// </summary>
    [Fact]
    public void Build_ShouldNormalizeEquivalentProviderContext()
    {
        var first = PromptCacheNamespaceBuilder.Build(" LLM ", "OpenAI", " Provider-A ", "GPT-5");
        var second = PromptCacheNamespaceBuilder.Build("llm", " openai ", "provider-a", " gpt-5 ");

        first.Should().Be(second);
    }

    /// <summary>
    /// 模型变化应进入不同缓存命名空间。
    /// </summary>
    [Fact]
    public void Build_ShouldChangeNamespace_WhenModelChanges()
    {
        var first = PromptCacheNamespaceBuilder.Build("llm", "openai", "provider-a", "gpt-5");
        var second = PromptCacheNamespaceBuilder.Build("llm", "openai", "provider-a", "gpt-5-mini");

        first.Should().NotBe(second);
    }

    /// <summary>
    /// Provider 配置变化应进入不同缓存命名空间。
    /// </summary>
    [Fact]
    public void Build_ShouldChangeNamespace_WhenProviderIdChanges()
    {
        var first = PromptCacheNamespaceBuilder.Build("llm", "openai", "provider-a", "gpt-5");
        var second = PromptCacheNamespaceBuilder.Build("llm", "openai", "provider-b", "gpt-5");

        first.Should().NotBe(second);
    }
}
