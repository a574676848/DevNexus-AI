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
    private readonly IToolCatalogService _toolCatalogService;

    /// <summary>
    /// 初始化工具调用参数预验证服务。
    /// </summary>
    public DefaultToolInvocationValidationService(IToolCatalogService toolCatalogService)
    {
        _toolCatalogService = toolCatalogService;
    }

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
            using var document = JsonDocument.Parse(argumentsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Invalid(AiOptimizationConstants.ToolValidationMessages.NonObjectArguments);
            }

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

        var pluginName = ResolvePluginName(toolName);
        if (arguments.Count == 0 && RequiresNonEmptyArguments(pluginName))
        {
            return Invalid(AiOptimizationConstants.ToolValidationMessages.EmptyArguments);
        }

        if (IsPlugin(pluginName, AiOptimizationConstants.ToolProtocol.HostServicePlugin)
            && AiOptimizationConstants.ToolValidation.FileArgumentKeys.Any(key => HasBlankString(arguments, key)))
        {
            return Invalid(AiOptimizationConstants.ToolValidationMessages.BlankFileArgument);
        }

        if ((IsPlugin(pluginName, AiOptimizationConstants.ToolProtocol.WebSearchPlugin)
                || IsPlugin(pluginName, AiOptimizationConstants.ToolProtocol.KnowledgeBasePlugin))
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

    private string ResolvePluginName(string toolName)
    {
        var parsedName = ToolInvocationNameParser.Parse(toolName);
        return _toolCatalogService.ResolvePluginName(parsedName.PluginName) ?? parsedName.PluginName;
    }

    private static bool IsPlugin(string pluginName, string expectedPluginName)
    {
        return string.Equals(pluginName, expectedPluginName, StringComparison.Ordinal);
    }

    private static bool RequiresNonEmptyArguments(string pluginName)
    {
        return IsPlugin(pluginName, AiOptimizationConstants.ToolProtocol.HostServicePlugin)
            || IsPlugin(pluginName, AiOptimizationConstants.ToolProtocol.CodeExecutionPlugin)
            || IsPlugin(pluginName, AiOptimizationConstants.ToolProtocol.WebSearchPlugin)
            || IsPlugin(pluginName, AiOptimizationConstants.ToolProtocol.KnowledgeBasePlugin);
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
