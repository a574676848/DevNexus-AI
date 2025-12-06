using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DevNexus.Core.Abstractions;
using DevNexus.Core.Services;
using DevNexus.Core.Services.LLM;
using DevNexus.Infrastructure.Configuration;

namespace DevNexus.Core.Extensions;

/// <summary>
/// Core 服务注册扩展
/// </summary>
public static class CoreServiceExtensions
{
    /// <summary>
    /// 注册 Core 层服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置对象</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddCoreServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 注册 LLM 配置选项
        services.Configure<LLMOptions>(configuration.GetSection("LLM"));
        
        // 注册 LLM 提供商工厂（单例）
        services.AddSingleton<LLMProviderFactory>();
        
        // 注册 KernelService（作用域）
        services.AddScoped<KernelService>();
        
        // 注册 Token 审计服务（单例）
        services.AddSingleton<TokenAuditService>();
        
        // 注册审批服务（单例，用于跨请求管理审批状态）
        services.AddSingleton<IApprovalService, ApprovalService>();
        
        return services;
    }
}
