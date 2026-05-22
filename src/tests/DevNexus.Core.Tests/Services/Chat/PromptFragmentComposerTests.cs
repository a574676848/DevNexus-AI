using DevNexus.Core.Services.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// Prompt 片段组合器测试。
/// </summary>
public sealed class PromptFragmentComposerTests
{
    /// <summary>
    /// 命名工厂应映射到稳定槽位。
    /// </summary>
    [Fact]
    public void NamedFactories_ShouldMapToStableSlots()
    {
        PromptFragment.SystemIdentity("identity").Slot.Should().Be(PromptSlot.SystemIdentity);
        PromptFragment.OutputContract("contract").Slot.Should().Be(PromptSlot.OutputContract);
        PromptFragment.ToolGuidance("tool").Slot.Should().Be(PromptSlot.ToolGuidance);
        PromptFragment.SecurityBoundary("security").Slot.Should().Be(PromptSlot.SecurityBoundary);
        PromptFragment.AgentWorkflow("workflow").Slot.Should().Be(PromptSlot.AgentWorkflow);
        PromptFragment.RepairInstruction("repair").Slot.Should().Be(PromptSlot.RepairInstruction);
        PromptFragment.DynamicContext("context", 0, PromptFragmentSources.DynamicMemory)
            .Slot.Should().Be(PromptSlot.DynamicContext);
    }

    /// <summary>
    /// 组合器应先按槽位排序，再按序号排序。
    /// </summary>
    [Fact]
    public void Compose_ShouldSortBySlotThenSequence()
    {
        var prompt = PromptFragmentComposer.Compose(
        [
            PromptFragment.AgentWorkflow("workflow"),
            PromptFragment.ToolGuidance("tool-b", sequence: 20),
            PromptFragment.SystemIdentity("identity"),
            PromptFragment.ToolGuidance("tool-a", sequence: 10),
            PromptFragment.OutputContract("contract"),
            PromptFragment.RepairInstruction("repair")
        ]);

        prompt.Should().Be(string.Join(
            Environment.NewLine,
            "identity",
            "contract",
            "tool-a",
            "tool-b",
            "workflow",
            "repair"));
    }

    /// <summary>
    /// 空白片段应被过滤，片段边界应统一为单个换行。
    /// </summary>
    [Fact]
    public void Compose_ShouldTrimAndFilterBlankFragments()
    {
        var prompt = PromptFragmentComposer.Compose(
        [
            PromptFragment.SystemIdentity(" identity "),
            PromptFragment.OutputContract("   "),
            PromptFragment.ToolGuidance($"{Environment.NewLine}tool{Environment.NewLine}")
        ]);

        prompt.Should().Be(string.Join(
            Environment.NewLine,
            "identity",
            "tool"));
    }
}
