using DevNexus.Core.Services;
using DevNexus.Core.Services.AuthUseCases;
using DevNexus.Core.Services.UserAdminUseCases;
using DevNexus.Core.Models.Execution;
using DevNexus.Domain.Configuration;
using DevNexus.Shared.Constants;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DevNexus.Core.Extensions;

/// <summary>
/// Core 服务注册扩展
/// </summary>
public static class CoreServiceExtensions
{
    /// <summary>
    /// 注册 Core 层服务
    /// </summary>
    public static IServiceCollection AddCoreServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 配置选项（保留在 Core，供 Domain.Configuration 使用）
        services.Configure<LLMOptions>(configuration.GetSection("LLM"));
        services.Configure<EmbeddingOptions>(configuration.GetSection("Embedding"));
        services.Configure<QdrantOptions>(configuration.GetSection("Qdrant"));
        services.Configure<BingSearchOptions>(configuration.GetSection("BingSearch"));
        services.Configure<GoogleSearchOptions>(configuration.GetSection("GoogleSearch"));
        services.Configure<CliPolicyOptions>(configuration.GetSection("CliPolicy"));
        // 注册审批服务（单例，用于跨请求管理审批状态）
        services.AddSingleton<IApprovalService, ApprovalService>();

        // 注册 Chat 重构后的服务
        services.AddScoped<Services.Chat.ChatSystemPromptBuilder>();
        services.AddScoped<Services.Chat.ChatHistorySummaryService>();
        services.AddScoped<Services.Chat.ChatHistoryMessageBuilder>();
        services.AddScoped<Services.Chat.ChatHistoryService>();
        services.AddScoped<Services.Chat.ChatPromptService>();
        services.AddScoped<Services.Chat.ChatSearchService>();
        services.AddScoped<Services.Chat.IChatSessionRuntimeInspector, Services.Chat.ChatSessionRuntimeInspector>();
        services.AddScoped<Services.Chat.ChatStreamingPreparationService>();
        services.AddScoped<Services.Chat.ChatStreamingFinalizer>();
        services.AddScoped<Services.Chat.IAgentLoopRecoveryMiddleware, Services.Chat.RuntimeRecoveryMiddleware>();
        services.AddScoped<Services.Chat.IAgentLoopRecoveryMiddleware, Services.Chat.LoopGuardMiddleware>();
        services.AddScoped<Services.Chat.AgentLoopRecoveryPipeline>();
        services.AddScoped<Services.Chat.IAgentLoopRecoveryGuard, Services.Chat.AgentLoopRecoveryGuard>();
        services.AddScoped<Services.Chat.ChatAgentLoopCoordinator>();
        services.AddScoped<Services.Chat.IPendingInteractionService, Services.Chat.PendingInteractionService>();
        services.AddScoped<Services.Chat.IChatSessionRuntimeService, Services.Chat.ChatSessionRuntimeService>();
        services.AddScoped<Services.Chat.ChatThinkingPersistenceCoordinator>();
        services.AddScoped<Services.Chat.ChatSwarmFinalizer>();
        services.AddScoped<Services.Chat.ToolBlockExecutionCoordinator>();
        services.AddScoped<ICliApprovalGrantService, CliApprovalGrantService>();
        services.AddScoped<ICliExecutionPolicyService, CliExecutionPolicyService>();
        services.AddScoped<Services.Chat.IChatSessionCleanupCoordinator, Services.Chat.ChatSessionCleanupCoordinator>();
        services.AddScoped<Services.Chat.IChatSessionDeletionCoordinator, Services.Chat.ChatSessionDeletionCoordinator>();
        services.AddScoped<Services.Chat.IChatMessageCompletionCoordinator, Services.Chat.ChatMessageCompletionCoordinator>();
        services.AddScoped<Services.Chat.ArtifactContextStrategy>();
        services.AddScoped<Services.Swarm.Context.IContextAnalyzer, Services.Swarm.Context.DefaultContextAnalyzer>();
        services.AddScoped<Services.Swarm.Context.IContextSegmenter, Services.Swarm.Context.DefaultContextSegmenter>();
        services.AddScoped<Services.Swarm.Execution.IWorkPackagePlanner, Services.Swarm.Execution.DefaultWorkPackagePlanner>();
        services.AddScoped<Services.Swarm.Execution.IExecutionStrategySelector, Services.Swarm.Execution.DefaultExecutionStrategySelector>();
        services.AddScoped<Services.Swarm.Execution.IContextWorkPackageExecutor, Services.Swarm.Execution.ContextWorkPackageExecutor>();
        services.AddScoped<Services.Swarm.Routing.IContextRoutingService, Services.Swarm.Routing.DefaultContextRoutingService>();
        services.AddScoped<Services.Swarm.Evaluation.IContextEvaluationService, Services.Swarm.Evaluation.DefaultContextEvaluationService>();
        services.AddScoped<Services.Swarm.ISwarmSessionControlService, Services.Swarm.SwarmSessionControlService>();
        services.AddScoped<Services.Swarm.ISwarmSessionViewService, Services.Swarm.SwarmSessionViewService>();
        services.AddScoped<Services.Swarm.Planning.ISwarmPackageScheduler, Services.Swarm.Planning.SwarmPackageScheduler>();

        // 注册 Artifact 服务
        services.AddScoped<IArtifactService, ArtifactService>();

        // 注册 Hangfire 后台任务
        var connectionString = configuration.GetConnectionString("devnexus");
        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString)));

            services.AddHangfireServer(options =>
            {
                options.WorkerCount = 5;
                options.ServerName = $"DevNexus-Worker-{Environment.MachineName}";
            });
        }

        // 注册 HttpClient
        services.AddHttpClient(HttpClientNames.BingSearch, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("User-Agent", "DevNexus-AI/1.0");
        });

        services.AddHttpClient(HttpClientNames.GoogleSearch, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("User-Agent", "DevNexus-AI/1.0");
        });

        services.AddHttpClient(HttpClientNames.DoubaoEmbedding, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Add("User-Agent", "DevNexus-AI/1.0");
            client.DefaultRequestHeaders.Add("Connection", "keep-alive");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            AllowAutoRedirect = false
        })
        .AddStandardResilienceHandler(options =>
        {
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(90);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.FromSeconds(1);
            options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
        });

        services.AddHttpClient(HttpClientNames.OpenAIEmbedding, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Add("User-Agent", "DevNexus-AI/1.0");
            client.DefaultRequestHeaders.Add("Connection", "keep-alive");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            AllowAutoRedirect = false
        })
        .AddStandardResilienceHandler(options =>
        {
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(90);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);

            // 熔断器采样时间必须 >= 尝试超时的 2 倍
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);

            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.FromSeconds(1);
            options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
        });

        services.AddHttpClient(HttpClientNames.LLMProvider, client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
            client.DefaultRequestHeaders.Add("User-Agent", "DevNexus-AI/1.0");
            client.DefaultRequestHeaders.Add("Connection", "keep-alive");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            // 允许自动解压缩响应以提高性能
            AutomaticDecompression = System.Net.DecompressionMethods.All
        })
        // 先移除 ServiceDefaults 注入的全局默认 resilience handler，
        // 再为 LLM 单独设置更长的超时策略，避免 30 秒默认值覆盖这里的配置。
#pragma warning disable EXTEXP0001
        .RemoveAllResilienceHandlers()
#pragma warning restore EXTEXP0001
        .AddStandardResilienceHandler(options =>
        {
            // 为 LLM Provider 配置更长的超时时间（Vision API 需要更多处理时间）
            // 总请求超时：3 分钟
            options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(3);
            // 单次尝试超时：2 分钟（允许重试）
            options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(2);
            // 熔断器配置 - SamplingDuration 必须 >= 2 × AttemptTimeout
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(5);
        });

        // 注册供应商管理服务
        services.AddProviderManagement();

        // 注册用户集成管理服务
        services.AddScoped<Abstractions.IIntegrationValidatorFactory, Services.Integrations.IntegrationValidatorFactory>();
        services.AddScoped<Abstractions.IIntegrationValidator, Services.Integrations.DefaultIntegrationValidator>();
        services.AddScoped<ILoginCommandHandler, LoginCommandHandler>();
        services.AddScoped<IRefreshTokenCommandHandler, RefreshTokenCommandHandler>();
        services.AddScoped<ILogoutCommandHandler, LogoutCommandHandler>();
        services.AddScoped<ILogoutAllDevicesCommandHandler, LogoutAllDevicesCommandHandler>();
        services.AddScoped<IChangePasswordCommandHandler, ChangePasswordCommandHandler>();
        services.AddScoped<IGetCurrentUserQueryHandler, GetCurrentUserQueryHandler>();
        services.AddScoped<IUpdateProfileCommandHandler, UpdateProfileCommandHandler>();
        services.AddScoped<IGetUsersQueryHandler, GetUsersQueryHandler>();
        services.AddScoped<IGetUserByIdQueryHandler, GetUserByIdQueryHandler>();
        services.AddScoped<ICreateUserCommandHandler, CreateUserCommandHandler>();
        services.AddScoped<IUpdateUserCommandHandler, UpdateUserCommandHandler>();
        services.AddScoped<IDeleteUserCommandHandler, DeleteUserCommandHandler>();
        services.AddScoped<IResetUserPasswordCommandHandler, ResetUserPasswordCommandHandler>();
        services.AddScoped<IToggleUserStatusCommandHandler, ToggleUserStatusCommandHandler>();
        services.AddScoped<IAuthApplicationService, AuthApplicationService>();
        services.AddScoped<IUserAdminApplicationService, UserAdminApplicationService>();
        services.AddScoped<IUserIntegrationService, UserIntegrationService>();
        // 注册版本发布与客户端更新平台服务
        services.AddScoped<IUpdateReleaseManagementService, UpdateReleaseManagementService>();
        services.AddScoped<IUpdateRolloutManagementService, UpdateRolloutManagementService>();
        services.AddScoped<IUpdateManifestService, UpdateManifestService>();
        services.AddScoped<IUpdateObservabilityService, UpdateObservabilityService>();
        services.AddScoped<IUpdateClientEventService, UpdateClientEventService>();

        return services;
    }

    /// <summary>
    /// 注册供应商管理服务（已迁移到 Infrastructure）
    /// </summary>
    public static IServiceCollection AddProviderManagement(
        this IServiceCollection services)
    {
        // 注意：供应商管理服务、用户管理服务等已迁移到 Infrastructure 层
        // 由 InfrastructureServiceExtensions 注册
        return services;
    }
}
