using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 工具调用参数预验证服务。
/// </summary>
public interface IToolInvocationValidationService
{
    /// <summary>
    /// 验证工具调用参数。
    /// </summary>
    ToolInvocationValidationResultDto Validate(string toolName, string argumentsJson);
}
