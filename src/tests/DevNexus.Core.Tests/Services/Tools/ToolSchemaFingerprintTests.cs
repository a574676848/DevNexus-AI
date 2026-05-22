using DevNexus.Core.Services.Tools;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Tools;

/// <summary>
/// 工具 Schema 指纹测试。
/// </summary>
public sealed class ToolSchemaFingerprintTests
{
    /// <summary>
    /// 工具列表顺序不应影响 Schema 指纹。
    /// </summary>
    [Fact]
    public void ComputeHash_ShouldIgnoreInputOrder()
    {
        var ordered = new[] { CreateTool("CodeExecution"), CreateTool("WebSearch") };
        var reversed = ordered.Reverse();

        var orderedHash = ToolSchemaFingerprint.ComputeHash(ordered);
        var reversedHash = ToolSchemaFingerprint.ComputeHash(reversed);

        orderedHash.Should().Be(reversedHash);
    }

    /// <summary>
    /// Schema 关键字段变化应改变指纹。
    /// </summary>
    [Fact]
    public void ComputeHash_ShouldChange_WhenSchemaFieldChanges()
    {
        var baseline = new[] { CreateTool("WebSearch") };
        var changed = new[] { CreateTool("WebSearch", resultContract: "新的结果契约") };

        ToolSchemaFingerprint.ComputeHash(baseline)
            .Should()
            .NotBe(ToolSchemaFingerprint.ComputeHash(changed));
    }

    /// <summary>
    /// 工具别名变化应改变 Schema 指纹。
    /// </summary>
    [Fact]
    public void ComputeHash_ShouldChange_WhenAliasesChange()
    {
        var baseline = new[] { CreateTool("WebSearchPlugin", aliases: ["WebSearch"]) };
        var changed = new[] { CreateTool("WebSearchPlugin", aliases: ["WebSearch", "web_search"]) };

        ToolSchemaFingerprint.ComputeHash(baseline)
            .Should()
            .NotBe(ToolSchemaFingerprint.ComputeHash(changed));
    }

    /// <summary>
    /// 统一输出标签要求变化应改变 Schema 指纹。
    /// </summary>
    [Fact]
    public void ComputeHash_ShouldChange_WhenTaggedOutputRequirementChanges()
    {
        var baseline = new[] { CreateTool("HostService", requiresTaggedOutput: false) };
        var changed = new[] { CreateTool("HostService", requiresTaggedOutput: true) };

        ToolSchemaFingerprint.ComputeHash(baseline)
            .Should()
            .NotBe(ToolSchemaFingerprint.ComputeHash(changed));
    }

    /// <summary>
    /// 并行执行能力变化应改变 Schema 指纹。
    /// </summary>
    [Fact]
    public void ComputeHash_ShouldChange_WhenParallelSupportChanges()
    {
        var baseline = new[] { CreateTool("WebSearch", supportsParallelExecution: false) };
        var changed = new[] { CreateTool("WebSearch", supportsParallelExecution: true) };

        ToolSchemaFingerprint.ComputeHash(baseline)
            .Should()
            .NotBe(ToolSchemaFingerprint.ComputeHash(changed));
    }

    /// <summary>
    /// Canonical Schema 应统一换行和首尾空白。
    /// </summary>
    [Fact]
    public void BuildCanonicalSchema_ShouldNormalizeWhitespaceAndLineEndings()
    {
        var windowsLineEnding = new[] { CreateTool("WebSearch", resultContract: " 契约\r\n说明 ") };
        var unixLineEnding = new[] { CreateTool("WebSearch", resultContract: "契约\n说明") };

        var first = ToolSchemaFingerprint.BuildCanonicalSchema(windowsLineEnding);
        var second = ToolSchemaFingerprint.BuildCanonicalSchema(unixLineEnding);

        first.Should().Be(second);
    }

    /// <summary>
    /// Canonical Schema 应包含工具调用协议版本。
    /// </summary>
    [Fact]
    public void BuildCanonicalSchema_ShouldIncludeProtocolVersion()
    {
        var schema = ToolSchemaFingerprint.BuildCanonicalSchema([CreateTool("WebSearch")]);

        schema.Should().StartWith(ToolSchemaFingerprint.ProtocolVersion);
    }

    private static ToolCatalogItemDto CreateTool(
        string pluginName,
        string resultContract = "返回摘要和来源。",
        IReadOnlyList<string>? aliases = null,
        bool requiresTaggedOutput = false,
        bool supportsParallelExecution = false)
    {
        return new ToolCatalogItemDto
        {
            PluginName = pluginName,
            DisplayName = pluginName,
            Category = AiOptimizationConstants.ToolCategories.Research,
            RiskLevel = AiOptimizationConstants.ToolRiskLevels.Low,
            ExposureMode = AiOptimizationConstants.ToolExposureModes.Direct,
            ResultContract = resultContract,
            RequiresTaggedOutput = requiresTaggedOutput,
            SupportsParallelExecution = supportsParallelExecution,
            Aliases = aliases ?? Array.Empty<string>()
        };
    }
}
