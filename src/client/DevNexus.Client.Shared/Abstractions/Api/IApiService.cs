namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// REST API 服务聚合接口
/// 继承自多个细分接口，统一暴露客户端 API 能力
/// </summary>
public interface IApiService :
    ISessionApiService,
    IProviderApiService,
    IAuditAnalyticsApiService,
    IUserApiService,
    ISystemApiService,
    IFilePlatformApiService,
    IArtifactApiService,
    IMemoryApiService,
    IUserIntegrationApiService,
    ISkillApiService
{
    // 聚合接口，无额外方法定义
    // 所有方法已在子接口中声明
}


