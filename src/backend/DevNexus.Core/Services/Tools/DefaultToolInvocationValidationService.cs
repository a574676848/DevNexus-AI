using System.Text.Json;
using DevNexus.Core.Abstractions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Tools;

/// <summary>
/// 默认工具调用参数预验证服务。
/// </summary>
public sealed class DefaultToolInvocationValidationService : IToolInvocationValidationService
{
    /// <inheritdoc />
    public ToolInvocationValidationResultDto Validate(string toolName, string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return Invalid(AiOptimizationConstants.ToolValidationMessages.MissingToolName);
        }

        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return Invalid(AiOptimizationConstants.ToolValidationMessages.MissingArguments);
        }

        Dictionary<string, JsonElement>? arguments;
        try
        {
            arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson);
        }
        catch (JsonException)
        {
            return Invalid(AiOptimizationConstants.ToolValidationMessages.InvalidJson);
        }

        if (arguments == null)
        {
            return Invalid(AiOptimizationConstants.ToolValidationMessages.EmptyArguments);
        }

        if (toolName.Contains(AiOptimizationConstants.ToolProtocol.HostServicePlugin, StringComparison.OrdinalIgnoreCase)
            && AiOptimizationConstants.ToolValidation.FileArgumentKeys.Any(key => HasBlankString(arguments, key)))
        {
            return Invalid(AiOptimizationConstants.ToolValidationMessages.BlankFileArgument);
        }

        if ((toolName.Contains(AiOptimizationConstants.ToolProtocol.WebSearchPlugin, StringComparison.OrdinalIgnoreCase)
                || toolName.Contains(AiOptimizationConstants.ToolProtocol.KnowledgeBasePlugin, StringComparison.OrdinalIgnoreCase))
            && AiOptimizationConstants.ToolValidation.QueryArgumentKeys.Any(key => HasBlankString(arguments, key)))
        {
            return Invalid(AiOptimizationConstants.ToolValidationMessages.BlankQueryArgument);
        }

        return new ToolInvocationValidationResultDto();
    }

    private static bool HasBlankString(IReadOnlyDictionary<string, JsonElement> arguments, string key)
    {
        return arguments.TryGetValue(key, out var value)
            && value.ValueKind == JsonValueKind.String
            && string.IsNullOrWhiteSpace(value.GetString());
    }

    private static ToolInvocationValidationResultDto Invalid(string message)
    {
        return new ToolInvocationValidationResultDto
        {
            IsValid = false,
            FailureReason = ToolFailureReason.ToolFormatError.ToWireValue(),
            UserMessage = message,
            Retryable = false
        };
    }
}
