using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevNexus.Core.Abstractions;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Integrations;

/// <summary>
/// 默认集成验证器实现 (基础检查)
/// </summary>
public class DefaultIntegrationValidator : IIntegrationValidator
{
    public IntegrationType SupportedType => IntegrationType.Custom; // 用于通用或自定义

    public async Task<ValidateUserIntegrationResponse> ValidateAsync(
        string? endpoint,
        IntegrationAuthType authType,
        string credential,
        string? secondaryCredential,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var response = new ValidateUserIntegrationResponse
        {
            IsValid = true,
            ValidatedAt = DateTime.UtcNow,
            Details = new Dictionary<string, object>
            {
                { "authType", authType.ToString() },
                { "endpoint", endpoint ?? "N/A" }
            }
        };

        if (string.IsNullOrEmpty(credential))
        {
            response.IsValid = false;
            response.ErrorMessage = "凭证不能为空";
        }

        return response;
    }
}

/// <summary>
/// 集成验证器工厂实现
/// </summary>
public class IntegrationValidatorFactory : IIntegrationValidatorFactory
{
    private readonly IEnumerable<IIntegrationValidator> _validators;

    public IntegrationValidatorFactory(IEnumerable<IIntegrationValidator> validators)
    {
        _validators = validators;
    }

    public IIntegrationValidator GetValidator(IntegrationType type)
    {
        var validator = _validators.FirstOrDefault(v => v.SupportedType == type);
        return validator ?? _validators.First(v => v is DefaultIntegrationValidator);
    }
}
