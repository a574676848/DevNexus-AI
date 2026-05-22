using DevNexus.Domain.Entities;
using DevNexus.Shared.Constants;
using DevNexus.Shared.Enums;
using System.Text;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 挂起交互恢复上下文构建器。
/// </summary>
internal static class PendingInteractionResumeContextBuilder
{
    private static readonly HashSet<string> InternalMetadataKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        PendingInteractionMetadataKeys.ResolutionAction,
        PendingInteractionMetadataKeys.ApprovalScope
    };

    /// <summary>
    /// 构建恢复上下文。
    /// </summary>
    public static string? Build(PendingInteraction? interaction)
    {
        if (interaction == null
            || interaction.Status != PendingInteractionStatus.Resolved
            || interaction.ResolutionData == null)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine("## 用户刚刚补充的关键信息");
        builder.AppendLine("以下信息由用户在挂起交互中刚刚补充，可直接用于继续执行上一次被中断的任务：");
        builder.AppendLine($"- 交互标题: {interaction.Title}");
        builder.AppendLine($"- 交互说明: {interaction.Description}");
        AppendResolutionMetadata(builder, interaction.ResolutionData);
        AppendUserProvidedValues(builder, interaction.ResolutionData);
        builder.AppendLine("请基于以上补充信息继续推进任务，不要重复向用户索取相同内容。");
        return builder.ToString();
    }

    private static void AppendResolutionMetadata(
        StringBuilder builder,
        IReadOnlyDictionary<string, object> resolutionData)
    {
        if (!resolutionData.TryGetValue(PendingInteractionMetadataKeys.ResolutionAction, out var actionValue))
        {
            return;
        }

        builder.AppendLine($"- 解决动作: {actionValue}");
        if (resolutionData.TryGetValue(PendingInteractionMetadataKeys.ApprovalScope, out var approvalScopeValue))
        {
            builder.AppendLine($"- 审批授权范围: {approvalScopeValue}");
        }
    }

    private static void AppendUserProvidedValues(
        StringBuilder builder,
        IReadOnlyDictionary<string, object> resolutionData)
    {
        foreach (var pair in resolutionData)
        {
            if (InternalMetadataKeys.Contains(pair.Key))
            {
                continue;
            }

            builder.AppendLine($"- {pair.Key}: {pair.Value}");
        }
    }
}
