using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 外部系统集成验证器接口
/// </summary>
public interface IIntegrationValidator
{
    /// <summary>
    /// 支持的集成类型
    /// </summary>
    IntegrationType SupportedType { get; }

    /// <summary>
    /// 执行验证
    /// </summary>
    Task<ValidateUserIntegrationResponse> ValidateAsync(
        string? endpoint,
        IntegrationAuthType authType,
        string credential,
        string? secondaryCredential,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 验证器工厂接口
/// </summary>
public interface IIntegrationValidatorFactory
{
    IIntegrationValidator GetValidator(IntegrationType type);
}
