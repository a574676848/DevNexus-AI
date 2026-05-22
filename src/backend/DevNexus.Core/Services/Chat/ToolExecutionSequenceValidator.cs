using System.Text.Json;
using DevNexus.Core.Models.Evaluation;
using DevNexus.Shared.Constants;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 工具执行序列验证结果。
/// </summary>
internal sealed record ToolExecutionSequenceValidationResult
{
    /// <summary>
    /// 序列是否有效。
    /// </summary>
    public bool IsValid { get; init; } = true;

    /// <summary>
    /// 失败提示。
    /// </summary>
    public string? Message { get; init; }
}

/// <summary>
/// 工具执行序列验证器。
/// </summary>
internal static class ToolExecutionSequenceValidator
{
    /// <summary>
    /// 验证同一轮 Agent Loop 内的工具调用标识是否稳定且唯一。
    /// </summary>
    public static ToolExecutionSequenceValidationResult Validate(IReadOnlyList<ToolExecutionRecord> toolRecords)
    {
        if (toolRecords.Count == 0)
        {
            return new ToolExecutionSequenceValidationResult();
        }

        if (toolRecords.Any(record => string.IsNullOrWhiteSpace(record.ToolName)))
        {
            return Invalid(AiOptimizationConstants.ToolValidationMessages.MissingToolName);
        }

        var argumentsValidation = ValidateArguments(toolRecords);
        if (!argumentsValidation.IsValid)
        {
            return argumentsValidation;
        }

        var recordsWithCallId = toolRecords
            .Where(record => record.ToolCallId.HasValue && record.ToolCallId.Value != Guid.Empty)
            .ToList();
        if (recordsWithCallId.Count != toolRecords.Count)
        {
            return Invalid(AiOptimizationConstants.ToolValidationMessages.MissingToolCallId);
        }

        var hasDuplicateCallId = recordsWithCallId
            .GroupBy(record => record.ToolCallId!.Value)
            .Any(group => group.Count() > 1);
        if (hasDuplicateCallId)
        {
            return Invalid(AiOptimizationConstants.ToolValidationMessages.DuplicateToolCallId);
        }

        return new ToolExecutionSequenceValidationResult();
    }

    private static ToolExecutionSequenceValidationResult Invalid(string message)
    {
        return new ToolExecutionSequenceValidationResult
        {
            IsValid = false,
            Message = message
        };
    }

    private static ToolExecutionSequenceValidationResult ValidateArguments(IReadOnlyList<ToolExecutionRecord> toolRecords)
    {
        foreach (var record in toolRecords)
        {
            if (string.IsNullOrWhiteSpace(record.Arguments))
            {
                return Invalid(AiOptimizationConstants.ToolValidationMessages.MissingArguments);
            }

            try
            {
                using var document = JsonDocument.Parse(record.Arguments);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return Invalid(AiOptimizationConstants.ToolValidationMessages.NonObjectArguments);
                }

                if (!record.Success && !document.RootElement.EnumerateObject().Any())
                {
                    return Invalid(AiOptimizationConstants.ToolValidationMessages.TruncatedArguments);
                }
            }
            catch (JsonException)
            {
                var message = ToolArgumentTruncationDetector.LooksTruncated(record.Arguments)
                    ? AiOptimizationConstants.ToolValidationMessages.TruncatedArguments
                    : AiOptimizationConstants.ToolValidationMessages.InvalidJson;
                return Invalid(message);
            }
        }

        return new ToolExecutionSequenceValidationResult();
    }
}
