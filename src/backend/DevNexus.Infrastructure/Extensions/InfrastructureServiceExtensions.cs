using DevNexus.Core.Abstractions.Search;
using DevNexus.Infrastructure.Models;
using DevNexus.Infrastructure.Services.Memory;
using DevNexus.Infrastructure.Services.Search;
using DevNexus.Infrastructure.Services.Evaluation;
using DevNexus.Infrastructure.Services.CliTerminal;
using DevNexus.Infrastructure.Services.Jobs;
using DevNexus.Infrastructure.Services.Plugins;
using DevNexus.Shared.Constants;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace DevNexus.Infrastructure.Extensions;

/// <summary>
/// Redis配置选项
/// </summary>
public class RedisOptions
{
    /// <summary>
    /// Redis连接字符串
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用Redis
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 实例名称
    /// </summary>
    public string InstanceName { get; set; } = "DevNexus";
}

/// <summary>
/// 基础设施服务注册扩展
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// 注册基础设施服务（使用 Aspire 客户端集成）
    /// </summary>
    /// <param name="builder">Host 应用构建器</param>
    /// <returns>Host 应用构建器</returns>
    public static TBuilder AddInfrastructureServices<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var configuration = builder.Configuration;
        var services = builder.Services;

        // 注册配置选项
        services.Configure<DatabaseOptions>(configuration.GetSection("Database"));
        services.Configure<RedisOptions>(configuration.GetSection("Redis"));
        services.Configure<SeqOptions>(configuration.GetSection("Seq"));
        services.Configure<CliSandboxOptions>(configuration.GetSection("CliSandbox"));

        // 手动注册 NpgsqlDataSource 以支持动态 JSON 序列化
        // Aspire 的 AddNpgsqlDbContext 不支持 configureDataSourceBuilder 参数，
        // 因此需要手动配置 NpgsqlDataSource 来启用 EnableDynamicJson()
        var connectionString = configuration.GetConnectionString(ConnectionStringNames.Database);
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);

        // 启用动态 JSON 序列化（Npgsql 8.0+ 需要显式启用）
        // 这是支持 Dictionary<string, object> 类型 JSONB 列所必需的
        dataSourceBuilder.EnableDynamicJson();

        var dataSource = dataSourceBuilder.Build();
        services.AddSingleton(dataSource);

        // 注册 DbContext，使用已配置的 NpgsqlDataSource
        var dbOptions = configuration.GetSection("Database").Get<DatabaseOptions>();
        services.AddDbContextPool<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(dataSource, npgsqlOptions =>
            {
                // 配置重试策略：处理瞬态故障（如超时）
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);

                // 设置命令超时（可选，默认 30s）
                npgsqlOptions.CommandTimeout(30);
            });

            if (dbOptions?.EnableDetailedLogging == true)
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        // 使用 Aspire 客户端集成注册 Redis
        // Aspire 会自动从 ConnectionStrings__redis 读取连接字符串
        builder.AddRedisClient(ConnectionStringNames.Redis);

        // 注册 Redis 分布式缓存
        // Aspire 的 AddRedisClient 注入的是 IConnectionMultiplexer，
        // 但 AddStackExchangeRedisCache 需要显式配置连接字符串
        var redisConnectionString = configuration.GetConnectionString(ConnectionStringNames.Redis);
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            var redisOptions = configuration.GetSection("Redis").Get<RedisOptions>();
            options.InstanceName = redisOptions?.InstanceName ?? "DevNexus";
        });

        // 使用 Aspire 客户端集成注册 Seq（禁用默认健康检查，因为健康检查端口与日志端口不同）
        // Aspire 会自动从 ConnectionStrings__seq 读取连接字符串
        builder.AddSeqEndpoint(ConnectionStringNames.Seq, configureSettings: settings =>
        {
            settings.DisableHealthChecks = true;
        });

        // 注册 ASP.NET Identity
        services.AddIdentityServices();

        // 注册 Hangfire
        services.AddHangfireServices(configuration);



        // 注册数据库健康检查和 Seq 健康检查（使用正确的健康检查端口 5342）
        var seqConnectionString = configuration.GetConnectionString(ConnectionStringNames.Seq);
        var seqHealthCheckUri = seqConnectionString?.Replace(":5341", ":5342/health");

        var healthChecksBuilder = services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>(name: "PostgreSQL");

        if (!string.IsNullOrEmpty(seqHealthCheckUri))
        {
            healthChecksBuilder.AddUrlGroup(new Uri(seqHealthCheckUri), name: "Seq", timeout: TimeSpan.FromSeconds(10));
        }

        // 注册 Infrastructure 层服务
        services.AddInfrastructureDomainServices(builder.Configuration);

        return builder;
    }

    /// <summary>
    /// 注册 Infrastructure 层的领域服务
    /// </summary>
    public static IServiceCollection AddInfrastructureDomainServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IExecutionStrategyExecutor, Services.Persistence.ExecutionStrategyExecutor>();
        services.AddScoped<IUnitOfWorkTransactionFactory, Services.Persistence.UnitOfWorkTransactionFactory>();

        // 注册加密服务
        services.AddSingleton<IEncryptionService, Services.EncryptionService>();

        // 注册缓存和分布式锁服务（已从 Core 迁移）
        // 注册缓存和分布式锁服务（已从 Core 迁移）
        services.AddScoped<IContextCacheService, Services.Cache.ContextCacheService>();
        services.AddSingleton<IDistributedLockService, Services.Cache.DistributedLockService>();

        // 注册用户管理服务（已从 Core 迁移）
        services.AddScoped<IUserManagementService, Services.UserManagementService>();
        services.AddScoped<IUserIdentityService, Services.Auth.UserIdentityService>();
        services.AddSingleton<IAuthTokenService, Services.Auth.JwtTokenService>();

        // 注册供应商管理服务（已从 Core 迁移）
        services.AddScoped<ILLMProviderManagementService, Services.Providers.LLMProviderManagementService>();
        services.AddScoped<IEmbeddingProviderManagementService, Services.Providers.EmbeddingProviderManagementService>();
        services.AddScoped<ISearchProviderManagementService, Services.Providers.SearchProviderManagementService>();
        services.AddScoped<IStorageProviderManagementService, Services.Providers.StorageProviderManagementService>();
        services.AddScoped<IModelPricingService, Services.Providers.ModelPricingService>();
        services.AddScoped<Services.Providers.ProviderSeederService>();

        // 注册审计分析服务
        services.AddScoped<Services.Analytics.AuditAnalyticsService>();
        services.AddScoped<IAuditAnalyticsReadService>(sp => sp.GetRequiredService<Services.Analytics.AuditAnalyticsService>());
        services.AddScoped<IAuditAnalyticsWriteService>(sp => sp.GetRequiredService<Services.Analytics.AuditAnalyticsService>());
        services.AddSingleton<IToolCatalogService, Services.Tools.InfrastructureToolCatalogService>();

        // 注册仓库服务
        services.AddScoped<IArtifactRepository, Repositories.ArtifactRepository>();
        services.AddScoped<IChatSessionRepository, Repositories.ChatSessionRepository>();
        services.AddScoped<IChatMessageRepository, Repositories.ChatMessageRepository>();
        services.AddScoped<IRefreshTokenStore, Repositories.RefreshTokenStore>();
        services.AddScoped<IUserIntegrationStore, Repositories.UserIntegrationStore>();
        services.AddScoped<IContextSwarmSessionRepository, Repositories.ContextSwarmSessionRepository>();
        services.AddScoped<ITerminalStreamRepository, Repositories.TerminalStreamRepository>();
        services.AddScoped<ICliApprovalGrantRepository, Repositories.CliApprovalGrantRepository>();
        services.AddScoped<ICliExecSessionRepository, Repositories.CliExecSessionRepository>();
        services.AddScoped<ICliExecCheckpointRepository, Repositories.CliExecCheckpointRepository>();
        services.AddScoped<IPendingInteractionRepository, Repositories.PendingInteractionRepository>();
        services.AddScoped<IUpdateReleaseRepository, Repositories.UpdateReleaseRepository>();
        services.AddScoped<IUpdateRolloutRepository, Repositories.UpdateRolloutRepository>();
        services.AddScoped<IUpdateClientEventRepository, Repositories.UpdateClientEventRepository>();
        services.AddScoped<IQueuedChatMessageRepository, Repositories.QueuedChatMessageRepository>();

        // 终端输出缓冲服务（Singleton 生命周期，全局共享）
        services.AddSingleton<ITerminalOutputBuffer, Core.Services.Terminal.TerminalOutputBuffer>();

        // 注册 LLM 相关服务
        services.AddSingleton<Services.LLM.LLMProviderCache>();
        services.AddSingleton<Services.LLM.LLMProviderCacheState>();
        services.AddScoped<ILLMProviderFactory, Services.LLM.LLMProviderFactory>();
        services.AddScoped<ITokenAuditService, Services.LLM.TokenAuditService>();
        services.AddSingleton<Services.LLM.ITokenAuditQueue, Services.LLM.TokenAuditQueue>();
        services.AddSingleton<Services.LLM.TokenAuditFilter>();
        // 🔧 注册工具执行收集过滤器 (P1-4 可观测性)
        services.AddSingleton<Services.LLM.ToolExecutionCollectorFilter>();
        services.AddHostedService<Services.LLM.TokenAuditBackgroundService>();
        services.AddScoped<IKernelService, Services.LLM.KernelService>();
        services.AddScoped<Services.LLM.KernelService>(sp => (Services.LLM.KernelService)sp.GetRequiredService<IKernelService>());

        // 注册 Embedding 相关服务
        services.AddScoped<IEmbeddingProviderFactory, Services.Embedding.EmbeddingProviderFactory>();
        services.AddScoped<Services.Embedding.EmbeddingProviderFactory>(sp => (Services.Embedding.EmbeddingProviderFactory)sp.GetRequiredService<IEmbeddingProviderFactory>());

        // 注册 Kernel Memory 服务
        services.AddSingleton<Core.Abstractions.IReasoningExtractionService, Services.LLM.ReasoningExtractionService>();
        services.AddKernelMemoryServices(configuration);

        // 注册 KnowledgeBase 服务（使用 Kernel Memory 实现）
        services.AddScoped<IKnowledgeBaseService, Services.KnowledgeBase.KnowledgeBaseService>();

        // 注册用户记忆服务
        services.AddScoped<IUserMemoryService, UserMemoryService>();

        // 注册智能体经验记忆服务
        services.AddScoped<Core.Abstractions.IAgentMemoryService, Services.Memory.AgentMemoryService>();

        // 注册会话临时记忆服务
        services.AddScoped<ISessionMemoryService, Services.Memory.SessionMemoryService>();

        // 注册代码解析服务
        services.AddScoped<IDocumentIntelligenceService, Services.Parsing.LocalDocumentIntelligenceService>();
        services.AddScoped<ICodeAnalysisService, Services.Parsing.LocalCodeAnalysisService>();

        // 注册文件存储服务
        services.AddScoped<Services.Storage.LocalFileStorageService>();
        services.AddScoped<Services.Storage.S3FileStorageService>();
        services.AddScoped<Services.Storage.CompositeFileStorageService>();
        services.AddScoped<IFileStorageService>(sp => sp.GetRequiredService<Services.Storage.CompositeFileStorageService>());

        // 注册文件平台持久化服务
        services.AddScoped<IUploadSessionService, Services.Files.PersistentUploadSessionService>();
        services.AddScoped<IFileAssetService, Services.Files.PersistentFileAssetService>();
        services.AddScoped<IFileTaskService, Services.Files.PersistentFileTaskService>();
        services.AddScoped<IFileOutputValidationService, Services.Files.FileOutputValidationService>();

        // 注册搜索引擎与阅读器
        services.AddScoped<ISearchEngine, SearXngSearchEngine>();
        services.AddScoped<ISearchEngine, TavilySearchEngine>();
        services.AddScoped<IWebReaderEngine, JinaReaderEngine>();
        services.AddScoped<IWebReaderEngine, FirecrawlReaderEngine>();

        // 注册 Semantic Kernel 插件
        services.AddScoped<Services.Plugins.WebSearchPlugin>();
        services.AddScoped<Services.Plugins.ImageGenerationPlugin>();
        services.AddScoped<Services.Plugins.IntegrationPlugin>();
        services.AddScoped<Services.Plugins.SessionMemoryPlugin>();
        services.AddScoped<Services.Jobs.ImageGenerationJob>();

        // 注册后台任务服务和 Jobs
        services.AddScoped<Services.Jobs.DocumentParsingJob>();
        services.AddScoped<Services.Jobs.CleanupJob>();
        services.AddScoped<Services.Jobs.SessionCleanupJob>();
        services.AddScoped<Services.Jobs.MemoryConsolidationJob>();
        services.AddScoped<Services.Jobs.MemoryConsolidationScanJob>();
        services.AddScoped<Services.Jobs.ExperienceDistillationJob>();
        services.AddScoped<Services.Jobs.ExperiencePruningJob>();
        services.AddScoped<IBackgroundJobService, Services.Jobs.BackgroundJobService>();

        // 注册文档解析服务
        services.AddPaddleOcrServices(configuration); // 新增: 注册 PaddleOCR
        services.AddDocumentParsingServices();

        // 显式注册 ISmartDocumentParser 代理到工厂
        services.AddScoped<ISmartDocumentParser>(sp => sp.GetRequiredService<Services.Parsing.SmartDocumentParserFactory>());

        // 注册 Swarm 服务
        services.AddScoped<Core.Services.Swarm.Analysis.IComplexityEvaluator, Core.Services.Swarm.Analysis.LlmComplexityEvaluator>();
        services.AddScoped<Core.Services.Swarm.Generation.IAgentGenerator, Core.Services.Swarm.Generation.AgentGenerator>();
        services.AddScoped<Core.Services.Swarm.Generation.IAgentFactory, Core.Services.Swarm.Generation.AgentFactory>();

        // Context & Tools
        services.AddScoped<Core.Services.Swarm.Context.IBlackboard, Core.Services.Swarm.Context.InMemoryBlackboard>();
        services.AddSingleton<Core.Abstractions.IToolRegistry, Core.Abstractions.InMemoryToolRegistry>();
        services.AddScoped<Core.Services.Swarm.Context.IContextSummarizer, Core.Services.Swarm.Context.LlmContextSummarizer>();

        // Swarm 重构子服务
        services.AddScoped<Core.Services.Swarm.Planning.ISwarmTaskExecutor, Core.Services.Swarm.Planning.SwarmTaskExecutor>();

        // ★ Singleton 会话注册表：跨 Scoped 实例共享控制状态（Pause/Resume/Abort）
        services.AddSingleton<Core.Services.Swarm.SwarmSessionRegistry>();
        services.AddSingleton<Core.Services.Swarm.Planning.AdaptiveConcurrencyController>();

        services.AddScoped<Core.Services.Swarm.Planning.DynamicTeamAssembler>();
        services.AddScoped<Core.Services.Swarm.Planning.GroupChatCoordinator>();
        services.AddScoped<Core.Services.Swarm.Planning.IDynamicToolSelector, Core.Services.Swarm.Planning.LlmDynamicToolSelector>();
        services.AddScoped<Core.Services.Swarm.Planning.ISwarmOrchestrator, Core.Services.Swarm.Planning.ContextDrivenSwarmOrchestrator>();

        // Evaluation — 统一响应评估与修复机制 (Phase 1 & 4)
        services.AddScoped<IRepairContextBuilder, Core.Services.Chat.AgentRepairPromptBuilder>();
        services.AddScoped<RuleBasedResponseEvaluator>();
        services.AddScoped<LlmResponseEvaluator>();
        services.AddScoped<IRuleResponseEvaluator, RuleBasedResponseEvaluator>();
        services.AddScoped<ILlmResponseEvaluator, LlmResponseEvaluator>();

        // Agent Loop Executor (Phase 5)
        services.AddScoped<Core.Services.Chat.AgentLoopExecutor>();

        // CLI Terminal Services (Phase 2)
        services.AddSingleton<CliEnvironmentService>();
        services.AddSingleton<CliDockerContextResolver>();
        services.AddSingleton<LocalRestrictedSandboxSessionProvider>();
        services.AddSingleton<ContainerSandboxSessionProvider>();
        services.AddSingleton<ICliSandboxSessionProvider, ConfigurableCliSandboxSessionProvider>();
        services.AddSingleton<ProcessCliRuntimeHost>(sp => (ProcessCliRuntimeHost)sp.GetRequiredService<Core.Abstractions.ICliProcessRegistry>());
        services.AddSingleton<ICliSandboxWarmPool, CliRuntimeWarmPool>();
        services.AddScoped<ICliSandboxValidationService, CliSandboxValidationService>();
        services.AddSingleton<Core.Abstractions.ICliProcessRegistry, ProcessCliRuntimeHost>();
        services.AddScoped<ICliExecCheckpointService, CliExecCheckpointService>();
        services.AddScoped<ICliRuntimeCoordinator, CliRuntimeCoordinator>();
        services.AddSingleton<CliSessionManager>();
        services.AddHostedService<CliReaperJob>();

        // Plugins (Phase 3)
        services.AddScoped<CodeExecutionPlugin>();

        // 默认 IResponseEvaluator 绑定到 LLM 评估器（Swarm 默认路径）
        // AgentLoop 通过 IRuleResponseEvaluator / ILlmResponseEvaluator 显式注入，避免歧义
        services.AddScoped<IResponseEvaluator, LlmResponseEvaluator>();

        // Handoff — 结构化交接协议
        services.AddScoped<Core.Services.Swarm.Handoff.IStructuredHandoffService, Core.Services.Swarm.Handoff.StructuredHandoffService>();

        // Routing — 动态路由模式 (Phase 2.2)
        services.AddScoped<Core.Services.Swarm.Routing.IAgentRouter, Core.Services.Swarm.Routing.LlmAgentRouter>();

        // Memory — 分层记忆系统 (Phase 2.3)
        services.AddScoped<Core.Services.Swarm.Memory.ITieredMemoryService, Core.Services.Swarm.Memory.TieredMemoryService>();

        // Safety & Confirmation
        services.AddSingleton<Core.Abstractions.IUserContextAccessor, Services.Systems.UserContextAccessor>();
        services.AddSingleton<Services.Systems.HostService>();
        services.AddSingleton<Core.Abstractions.IHostStructuredService>(sp => sp.GetRequiredService<Services.Systems.HostService>());
        services.AddSingleton<Core.Abstractions.ICliExecService>(sp => sp.GetRequiredService<Services.Systems.HostService>());

        // 用户存储路径服务
        services.AddSingleton<Core.Abstractions.IUserStoragePathService, Services.Systems.UserStoragePathService>();

        // 注册 Skill 系统服务
        services.AddSingleton<Core.Abstractions.ISkillRegistry, Services.Skills.SkillRegistry>();
        services.AddScoped<Core.Abstractions.ISkillMatcher, Services.Skills.SkillMatcher>();
        services.AddSingleton<Core.Abstractions.ISkillRuntimePathResolver, Services.Skills.SkillRuntimePathResolver>();
        services.AddSingleton<Services.Skills.PluginResolver>();
        services.AddSingleton<Core.Abstractions.IPluginResolver>(sp => sp.GetRequiredService<Services.Skills.PluginResolver>());
        services.AddHostedService<Services.Skills.SkillFileWatcherService>();

        // Swarm 会话崩溃恢复服务 (Phase 3.1)
        services.AddHostedService<Core.Services.Swarm.SwarmSessionRecoveryService>();

        return services;
    }

    /// <summary>
    /// 注册文档解析服务
    /// 使用 Kernel Memory 和 Vision Service 处理文档，保留代码解析器
    /// </summary>
    private static IServiceCollection AddDocumentParsingServices(this IServiceCollection services)
    {
        services.AddSingleton<Services.Parsing.FileMimeValidationService>();

        // 注册代码解析器（保留：提供 AST 分析能力）
        services.AddScoped<Services.Parsing.CodeDocumentParser>();

        // 注册新适配器
        services.AddScoped<Services.Parsing.KernelMemoryDocumentParser>();
        services.AddScoped<Services.Parsing.ImageDocumentParser>();

        // 注册 Vision 解析服务（保留：提供图片识别能力）
        services.AddScoped<Services.Parsing.VisionParsingService>();

        // 注册 OCR 整理服务
        services.AddScoped<Services.Parsing.IOcrResultOrganizer, Services.Parsing.OcrResultOrganizer>();

        // 注册解析器工厂
        services.AddScoped<Services.Parsing.SmartDocumentParserFactory>(sp =>
        {
            var parsers = new List<ISmartDocumentParser>
            {
                sp.GetRequiredService<Services.Parsing.CodeDocumentParser>(),
                sp.GetRequiredService<Services.Parsing.KernelMemoryDocumentParser>(), // PDF/Word/Excel
                sp.GetRequiredService<Services.Parsing.ImageDocumentParser>()        // Image
            };
            return new Services.Parsing.SmartDocumentParserFactory(parsers, sp.GetRequiredService<ILogger<Services.Parsing.SmartDocumentParserFactory>>());
        });

        return services;
    }

    /// <summary>
    /// 注册 PaddleOCR 服务
    /// </summary>
    public static IServiceCollection AddPaddleOcrServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. 配置 Options
        services.Configure<Services.Parsing.PaddleOCR.PaddleOcrOptions>(options =>
        {
            // 优先从连接字符串读取 Endpoint (Aspire 方式)
            var connectionString = configuration.GetConnectionString(ConnectionStringNames.PaddleOcr);
            if (!string.IsNullOrEmpty(connectionString))
            {
                // Connection string 可能是纯 URL，也可能是 Key=Value 格式？
                // Aspire AddConnectionString("paddle-ocr") 只是传递字符串
                // 通常外部 URL 资源就是 URL 本身
                options.Endpoint = connectionString;
            }
            else
            {
                // 回退到 Section 配置
                var section = configuration.GetSection(Services.Parsing.PaddleOCR.PaddleOcrOptions.SectionName);
                if (section.Exists())
                {
                    options.Endpoint = section["Endpoint"] ?? string.Empty;
                }
            }
        });

        // 2. 注册 HttpClient
        services.AddHttpClient<Services.Parsing.PaddleOCR.IPaddleOcrClient, Services.Parsing.PaddleOCR.PaddleOcrClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<Services.Parsing.PaddleOCR.PaddleOcrOptions>>().Value;
            if (!string.IsNullOrEmpty(options.Endpoint))
            {
                client.BaseAddress = new Uri(options.Endpoint);
            }
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        })
        .AddStandardResilienceHandler(); // 使用标准弹性策略 (重试、熔断等)

        return services;
    }



    /// <summary>
    /// 注册 Hangfire 服务
    /// </summary>
    public static IServiceCollection AddHangfireServices(this IServiceCollection services, IConfiguration configuration)
    {
        var dbConnectionString = configuration.GetConnectionString(ConnectionStringNames.Database)
                                 ?? configuration.GetConnectionString("Postgres");

        if (!string.IsNullOrEmpty(dbConnectionString))
        {
            services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(dbConnectionString)));

            services.AddHangfireServer();
        }

        return services;
    }

    /// <summary>
    /// 获取 Redis 连接字符串
    /// </summary>
    /// <param name="configuration">配置对象</param>
    /// <returns>Redis 连接字符串，优先从 Aspire 注入的 ConnectionStrings 读取</returns>
    public static string? GetRedisConnectionString(this IConfiguration configuration)
    {
        // 优先从 Aspire 注入的 ConnectionStrings 读取
        var aspireRedisConnection = configuration.GetConnectionString(ConnectionStringNames.Redis);
        if (!string.IsNullOrEmpty(aspireRedisConnection))
        {
            return aspireRedisConnection;
        }

        // 回退到传统配置
        var redisOptions = configuration.GetSection("Redis").Get<RedisOptions>();
        return redisOptions?.Enabled == true ? redisOptions.ConnectionString : null;
    }



    /// <summary>
    /// 注册 ASP.NET Identity 服务
    /// </summary>
    /// <param name="services">服务集合</param>
    private static void AddIdentityServices(this IServiceCollection services)
    {
        services.AddIdentity<InfrastructureUser, IdentityRole<Guid>>(options =>
        {
            // 密码策略
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 6;
            options.Password.RequiredUniqueChars = 1;

            // 锁定策略
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            // 用户策略
            options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
            options.User.RequireUniqueEmail = true;

            // 登录策略
            options.SignIn.RequireConfirmedEmail = false;
            options.SignIn.RequireConfirmedPhoneNumber = false;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();
    }

    /// <summary>
    /// 应用数据库迁移并执行种子数据（异步版本）
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    public static async Task ApplyDatabaseMigrationsAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var dbOptions = services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var logger = services.GetRequiredService<ILogger<ApplicationDbContext>>();

        if (!dbOptions.EnableAutoMigration)
        {
            logger.LogInformation("[Database] 已禁用自动迁移，跳过数据库升级检查。");
            return;
        }

        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        var connectionString = dbContext.Database.GetConnectionString();
        var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();
        var appliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync()).ToList();

        logger.LogInformation(
            "[Database] 自动迁移检查开始 | Database={Database} AppliedCount={AppliedCount} PendingCount={PendingCount}",
            dbContext.Database.GetDbConnection().Database,
            appliedMigrations.Count,
            pendingMigrations.Count);

        if (pendingMigrations.Count > 0)
        {
            logger.LogInformation(
                "[Database] 检测到待应用迁移 | Migrations={Migrations}",
                string.Join(", ", pendingMigrations));
        }
        else
        {
            logger.LogInformation("[Database] 未检测到待应用迁移。");
        }

        try
        {
            await dbContext.Database.MigrateAsync();

            var finalAppliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync()).ToList();
            logger.LogInformation(
                "[Database] 自动迁移完成 | Database={Database} AppliedCount={AppliedCount}",
                dbContext.Database.GetDbConnection().Database,
                finalAppliedMigrations.Count);
        }
        catch (Exception ex)
        {
            logger.LogCritical(
                ex,
                "[Database] 自动迁移失败 | Database={Database} PendingMigrations={PendingMigrations} Connection={Connection}",
                dbContext.Database.GetDbConnection().Database,
                pendingMigrations.Count == 0 ? "无" : string.Join(", ", pendingMigrations),
                connectionString);
            throw;
        }

        // 执行种子数据
        await serviceProvider.SeedDatabaseAsync();
    }
}
