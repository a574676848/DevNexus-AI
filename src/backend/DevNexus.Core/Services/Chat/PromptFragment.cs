using System.Text;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 稳定 Prompt 片段槽位。
/// </summary>
internal enum PromptSlot
{
    /// <summary>
    /// 系统身份。
    /// </summary>
    SystemIdentity = 0,

    /// <summary>
    /// 输出契约。
    /// </summary>
    OutputContract = 10,

    /// <summary>
    /// 工具使用规则。
    /// </summary>
    ToolGuidance = 20,

    /// <summary>
    /// 安全边界。
    /// </summary>
    SecurityBoundary = 30,

    /// <summary>
    /// Agent 工作流。
    /// </summary>
    AgentWorkflow = 40,

    /// <summary>
    /// 修复指令。
    /// </summary>
    RepairInstruction = 50,

    /// <summary>
    /// 动态上下文。
    /// </summary>
    DynamicContext = 60
}

/// <summary>
/// Prompt 片段来源标识。
/// </summary>
internal static class PromptFragmentSources
{
    /// <summary>
    /// 未知来源。
    /// </summary>
    public const string Unknown = "unknown";

    /// <summary>
    /// 系统身份来源。
    /// </summary>
    public const string SystemIdentity = "chat.system_identity";

    /// <summary>
    /// 输出契约来源。
    /// </summary>
    public const string OutputContract = "prompt_constants.output_contract";

    /// <summary>
    /// 工具使用规则来源。
    /// </summary>
    public const string ToolGuidance = "prompt_constants.tool_guidance";

    /// <summary>
    /// 文件安全边界来源。
    /// </summary>
    public const string SecurityBoundary = "prompt_constants.security_boundary";

    /// <summary>
    /// Agent 工作流来源。
    /// </summary>
    public const string AgentWorkflow = "prompt_constants.agent_workflow";

    /// <summary>
    /// 自动修复指令来源。
    /// </summary>
    public const string RepairInstruction = "agent_repair_prompt";

    /// <summary>
    /// 动态记忆上下文来源。
    /// </summary>
    public const string DynamicMemory = "dynamic.memory";

    /// <summary>
    /// 动态系统经验上下文来源。
    /// </summary>
    public const string DynamicSystemExperience = "dynamic.system_experience";

    /// <summary>
    /// 动态工具选择上下文来源。
    /// </summary>
    public const string DynamicToolSelection = "dynamic.tool_selection";

    /// <summary>
    /// 动态挂起交互上下文来源。
    /// </summary>
    public const string DynamicPendingInteraction = "dynamic.pending_interaction";

    /// <summary>
    /// 动态 Skill 上下文来源。
    /// </summary>
    public const string DynamicSkill = "dynamic.skill";

    /// <summary>
    /// 动态会话记忆上下文来源。
    /// </summary>
    public const string DynamicSessionMemory = "dynamic.session_memory";
}

/// <summary>
/// 稳定 Prompt 片段。
/// </summary>
internal sealed record PromptFragment(PromptSlot Slot, int Sequence, string Text, string Source)
{
    /// <summary>
    /// 创建 Prompt 片段。
    /// </summary>
    public static PromptFragment Create(
        PromptSlot slot,
        string text,
        int sequence = 0,
        string? source = null)
    {
        return new PromptFragment(slot, sequence, text, NormalizeSource(source));
    }

    /// <summary>
    /// 创建系统身份片段。
    /// </summary>
    public static PromptFragment SystemIdentity(string text, int sequence = 0)
    {
        return Create(PromptSlot.SystemIdentity, text, sequence, PromptFragmentSources.SystemIdentity);
    }

    /// <summary>
    /// 创建输出契约片段。
    /// </summary>
    public static PromptFragment OutputContract(string text, int sequence = 0)
    {
        return Create(PromptSlot.OutputContract, text, sequence, PromptFragmentSources.OutputContract);
    }

    /// <summary>
    /// 创建工具使用规则片段。
    /// </summary>
    public static PromptFragment ToolGuidance(string text, int sequence = 0)
    {
        return Create(PromptSlot.ToolGuidance, text, sequence, PromptFragmentSources.ToolGuidance);
    }

    /// <summary>
    /// 创建安全边界片段。
    /// </summary>
    public static PromptFragment SecurityBoundary(string text, int sequence = 0)
    {
        return Create(PromptSlot.SecurityBoundary, text, sequence, PromptFragmentSources.SecurityBoundary);
    }

    /// <summary>
    /// 创建 Agent 工作流片段。
    /// </summary>
    public static PromptFragment AgentWorkflow(string text, int sequence = 0)
    {
        return Create(PromptSlot.AgentWorkflow, text, sequence, PromptFragmentSources.AgentWorkflow);
    }

    /// <summary>
    /// 创建修复指令片段。
    /// </summary>
    public static PromptFragment RepairInstruction(string text, int sequence = 0)
    {
        return Create(PromptSlot.RepairInstruction, text, sequence, PromptFragmentSources.RepairInstruction);
    }

    /// <summary>
    /// 创建动态上下文片段。
    /// </summary>
    public static PromptFragment DynamicContext(string text, int sequence, string source)
    {
        return Create(PromptSlot.DynamicContext, text, sequence, source);
    }

    private static string NormalizeSource(string? source)
    {
        return string.IsNullOrWhiteSpace(source)
            ? PromptFragmentSources.Unknown
            : source.Trim();
    }
}

/// <summary>
/// Prompt 片段组合器。
/// </summary>
internal static class PromptFragmentComposer
{
    /// <summary>
    /// 按槽位和序号生成稳定 Prompt 前缀。
    /// </summary>
    public static string Compose(IEnumerable<PromptFragment> fragments)
    {
        var builder = new StringBuilder();
        var orderedFragments = fragments
            .Where(fragment => !string.IsNullOrWhiteSpace(fragment.Text))
            .OrderBy(fragment => fragment.Slot)
            .ThenBy(fragment => fragment.Sequence)
            .ToList();

        foreach (var fragment in orderedFragments)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(fragment.Text.Trim());
        }

        return builder.ToString();
    }
}
