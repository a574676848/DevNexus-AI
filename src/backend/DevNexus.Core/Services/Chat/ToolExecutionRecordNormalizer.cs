using DevNexus.Core.Models.Evaluation;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 工具执行记录归一化器。
/// 负责为半状态或信息缺失的工具记录补齐基础语义，避免评估链直接消费脏数据。
/// </summary>
internal static class ToolExecutionRecordNormalizer
{
    private const int MaxErrorSummaryLength = 200;

    public static List<ToolExecutionRecord> Normalize(IReadOnlyCollection<ToolExecutionRecord> toolRecords)
    {
        if (toolRecords.Count == 0)
        {
            return new List<ToolExecutionRecord>();
        }

        return toolRecords.Select(Normalize).ToList();
    }

    private static ToolExecutionRecord Normalize(ToolExecutionRecord record)
    {
        if (record.Success)
        {
            return record with
            {
                ErrorSummary = string.IsNullOrWhiteSpace(record.ErrorSummary) ? null : record.ErrorSummary,
                ErrorMessage = string.IsNullOrWhiteSpace(record.ErrorMessage) ? null : record.ErrorMessage,
                UserMessage = string.IsNullOrWhiteSpace(record.UserMessage) ? null : record.UserMessage
            };
        }

        var errorSummary = ResolveErrorSummary(record);
        var userMessage = ResolveUserMessage(record, errorSummary);
        var suggestedAction = ResolveSuggestedAction(record);

        return record with
        {
            ErrorSummary = errorSummary,
            UserMessage = userMessage,
            SuggestedAction = suggestedAction,
            RequestedUserInputLabel = ResolveRequestedInputLabel(record, userMessage)
        };
    }

    private static string ResolveErrorSummary(ToolExecutionRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.ErrorSummary))
        {
            return record.ErrorSummary!;
        }

        if (!string.IsNullOrWhiteSpace(record.UserMessage))
        {
            return record.UserMessage!;
        }

        if (!string.IsNullOrWhiteSpace(record.ErrorMessage))
        {
            return ToolOutputBudgetCompressor.Compress(record.ErrorMessage!, MaxErrorSummaryLength);
        }

        if (!string.IsNullOrWhiteSpace(record.Output))
        {
            return ToolOutputBudgetCompressor.Compress(record.Output!, MaxErrorSummaryLength);
        }

        return "工具执行失败，但没有返回可用的错误摘要。";
    }

    private static string? ResolveUserMessage(ToolExecutionRecord record, string errorSummary)
    {
        if (!string.IsNullOrWhiteSpace(record.UserMessage))
        {
            return record.UserMessage;
        }

        if (record.RequiresHumanIntervention)
        {
            return record.SuggestedAction switch
            {
                ToolSuggestedAction.RequestApproval => "当前操作需要你审批后才能继续执行。",
                ToolSuggestedAction.RefreshCredential => "当前操作需要你补充或刷新凭证后才能继续执行。",
                ToolSuggestedAction.PromptUserInput => "当前操作需要你补充输入后才能继续执行。",
                _ => "当前操作需要你补充前置条件后才能继续执行。"
            };
        }

        return errorSummary;
    }

    private static ToolSuggestedAction ResolveSuggestedAction(ToolExecutionRecord record)
    {
        if (record.SuggestedAction != ToolSuggestedAction.None)
        {
            return record.SuggestedAction;
        }

        if (record.RequiresHumanIntervention)
        {
            return record.FailureReason switch
            {
                ToolFailureReason.ApprovalRequired => ToolSuggestedAction.RequestApproval,
                ToolFailureReason.AuthExpired
                    or ToolFailureReason.AuthPermanent => ToolSuggestedAction.RefreshCredential,
                _ => ToolSuggestedAction.PromptUserInput
            };
        }

        return record.SuggestedAction;
    }

    private static string? ResolveRequestedInputLabel(ToolExecutionRecord record, string? userMessage)
    {
        if (!string.IsNullOrWhiteSpace(record.RequestedUserInputLabel))
        {
            return record.RequestedUserInputLabel;
        }

        if (!record.RequiresHumanIntervention)
        {
            return record.RequestedUserInputLabel;
        }

        return userMessage;
    }
}
