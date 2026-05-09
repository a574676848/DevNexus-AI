using DevNexus.ApiService.Hubs;
using DevNexus.ApiService.Middlewares;
using DevNexus.ApiService.Services;
using DevNexus.Core.Abstractions;
using DevNexus.Core.Extensions;
using DevNexus.Core.Services;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Configuration;
using DevNexus.Infrastructure.Extensions;
using DevNexus.Infrastructure.Services;
using DevNexus.Infrastructure.Services.Providers;
using DevNexus.Shared.Constants;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add infrastructure services using Aspire client integrations
// 使用 Aspire 客户端集成注册数据库、Redis、Seq
builder.AddInfrastructureServices();

// Add observability services (Distributed Tracing, Metrics, Structured Logging)
// 注册可观测性服务（分布式追踪、指标、结构化日志）
builder.Services.AddObservabilityServices();

// Add core services (LLM, Kernel)
builder.Services.AddCoreServices(builder.Configuration);

// Add Distributed Cache (Memory as default, can be replaced by Redis)
builder.Services.AddDistributedMemoryCache();

// Add core services
builder.Services.AddScoped<IChatService, ChatService>();
// 排队消息服务
builder.Services.AddScoped<IChatQueueService, ChatQueueService>();
// ChatQueueDispatcher 为 Singleton，内部使用 IServiceScopeFactory 获取 Scoped 服务
builder.Services.AddSingleton<ChatQueueDispatcher>();
builder.Services.AddSingleton<IChatQueueDispatcher>(sp => sp.GetRequiredService<ChatQueueDispatcher>());
// SignalR 桥接器：订阅 Core 层 Dispatcher 事件并推送到客户端
builder.Services.AddHostedService<QueueDispatcherSignalRBridge>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IArtifactStatusPublisher, ArtifactStatusPublisher>();
builder.Services.AddScoped<IClientNotifier, ClientNotifier>();
builder.Services.AddScoped<IToolInvocationNotifier, ToolInvocationNotifier>();
builder.Services.AddSingleton<IRuntimeEventNotifier, RuntimeEventNotifier>();
builder.Services.AddHostedService<PendingInteractionExpirationService>();
// ⚠️ TerminalNotifier 改为 Singleton：被 HostService(Singleton) 依赖，且本身是无状态通知器
builder.Services.AddSingleton<ITerminalNotifier, TerminalNotifier>();
// 注册 Swarm 事件服务 (Singleton 安全且跨 Scope)
builder.Services.AddSingleton<ISwarmEventService, SwarmEventService>();

// 注册确认服务 (依赖 ISwarmEventService)
builder.Services.AddSingleton<IConfirmationService, DevNexus.Infrastructure.Services.Systems.ConfirmationService>();

// 注册配置选项
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<EncryptionOptions>(builder.Configuration.GetSection("Encryption"));
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection("RateLimit"));
builder.Services.Configure<MonitoringOptions>(builder.Configuration.GetSection("Monitoring"));
builder.Services.Configure<CodeRAGOptions>(builder.Configuration.GetSection("CodeRAG"));
builder.Services.Configure<ElasticsearchOptions>(builder.Configuration.GetSection("Elasticsearch"));

// 注册 Elasticsearch 搜索服务
builder.Services.AddSingleton<IElasticsearchSearchService, ElasticsearchSearchService>();

// 验证 JWT 配置
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
try
{
    jwtOptions.Validate();
}
catch (InvalidOperationException ex)
{
    throw new InvalidOperationException($"JWT configuration validation failed: {ex.Message}", ex);
}

// Add SignalR support with Redis backplane
var signalRBuilder = builder.Services.AddSignalR(options =>
{
    // 配置 AI 长响应场景的超时设置
    // 客户端超时：6 分钟（比客户端的 ServerTimeout 略长）
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(6);
    // 心跳间隔：30 秒（保持连接活跃）
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
    // 启用详细错误信息（开发环境调试用）
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

// 配置 Redis 背板支持多实例部署
var redisConnectionString = builder.Configuration.GetRedisConnectionString();
if (!string.IsNullOrEmpty(redisConnectionString))
{
    signalRBuilder.AddStackExchangeRedis(redisConnectionString, options =>
    {
        options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("DevNexus");
    });
}

// Add authentication and authorization services
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    var key = Encoding.UTF8.GetBytes(jwtOptions.Key);
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidAudience = jwtOptions.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var authLogger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("JwtAuth");
            var authHeader = context.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(authHeader))
            {
                authLogger.LogInformation(
                    "[JwtAuth] 收到 Authorization 头 | Path={Path} | Prefix={Prefix}",
                    context.Request.Path,
                    authHeader.Split(' ').FirstOrDefault());
            }

            // 从 SignalR 连接查询字符串中获取令牌（所有 Hub 均需处理）
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) &&
                (path.StartsWithSegments("/chat-hub") ||
                 path.StartsWithSegments("/swarm-hub") ||
                 path.StartsWithSegments("/artifact-hub")))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var authLogger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("JwtAuth");
            var subject = context.Principal?.FindFirst("sub")?.Value
                ?? context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            authLogger.LogInformation(
                "[JwtAuth] Token 验证成功 | Path={Path} | Subject={Subject}",
                context.Request.Path,
                subject);
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            var authLogger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("JwtAuth");
            authLogger.LogWarning(
                context.Exception,
                "[JwtAuth] Token 验证失败 | Path={Path} | Message={Message}",
                context.Request.Path,
                context.Exception.Message);
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            var authLogger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("JwtAuth");
            authLogger.LogWarning(
                "[JwtAuth] 发起 Challenge | Path={Path} | Error={Error} | Description={Description}",
                context.Request.Path,
                context.Error,
                context.ErrorDescription);
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.AdminOnly, policy =>
        policy.RequireRole(RoleNames.Admin));
});

// Add health checks (Aspire 客户端集成会自动添加数据库和 Redis 健康检查)
// 无需手动配置

// Add services to the container.
builder.Services.AddProblemDetails();

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevNexusPolicy", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // 开发环境：允许所有源、所有方法、所有头
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            // 生产环境：允许同主域名的所有子域，加上配置的 AllowedOrigins
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
                ?? Array.Empty<string>();
            
            // 从配置或环境变量获取 API 主机名（如：example.com）
            var apiHost = builder.Configuration["Cors:ApiHost"] 
                ?? Environment.GetEnvironmentVariable("CORS_API_HOST");
            
            policy.SetIsOriginAllowed(origin =>
            {
                try
                {
                    var originUri = new Uri(origin);
                    var originHost = originUri.Host;

                    // 1. 允许已配置的 AllowedOrigins
                    if (allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    // 2. 如果配置了 API Host，允许同主域名的所有子域
                    if (!string.IsNullOrWhiteSpace(apiHost))
                    {
                        return originHost.Equals(apiHost, StringComparison.OrdinalIgnoreCase) ||
                               originHost.EndsWith("." + apiHost, StringComparison.OrdinalIgnoreCase);
                    }

                    return false;
                }
                catch
                {
                    return false;
                }
            })
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();  // SignalR 需要允许凭据
        }
    });
});

builder.Services.AddControllers();

// 配置 Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DevNexus AI API",
        Version = "v1",
        Description = "智能研发工作站 API 文档 - 提供实时 AI 对话、代码生成、文档管理等功能",
        Contact = new OpenApiContact
        {
            Name = "DevNexus Team",
            Email = "dev@devnexus.ai"
        },
        License = new OpenApiLicense
        {
            Name = "MIT License",
            Url = new Uri("https://opensource.org/licenses/MIT")
        }
    });

    // 启用 XML 注释
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }

    // 配置 JWT 认证
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "DevNexus AI API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "DevNexus AI API Documentation";
    });

    // Hangfire Dashboard (仅开发环境)
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = Array.Empty<Hangfire.Dashboard.IDashboardAuthorizationFilter>()
    });
}

app.UseHttpsRedirection();

// 启用静态文件服务（用于本地存储模式访问 wwwroot/uploads）
app.UseStaticFiles();

// 启用 CORS（必须在 UseAuthentication 之前）
app.UseCors("DevNexusPolicy");

app.UseAuthentication();
app.UseMiddleware<UserContextMiddleware>();

// 认证之后再做用户级监控与限流，才能拿到当前已认证用户。
app.UseMiddleware<MonitoringMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();

// 添加输入验证中间件
app.UseMiddleware<InputValidationMiddleware>();

app.UseAuthorization();

// Map controllers
app.MapControllers();

// Map SignalR hubs
app.MapHub<ChatHub>("/chat-hub");
app.MapHub<ArtifactHub>("/artifact-hub");
app.MapHub<SwarmHub>("/swarm-hub");

// Apply database migrations and seed data
await app.Services.ApplyDatabaseMigrationsAsync();

// 验证加密配置并运行供应商种子数据
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // 验证加密配置
    try
    {
        var encryptionOptions = scope.ServiceProvider.GetRequiredService<IOptions<EncryptionOptions>>().Value;
        encryptionOptions.Validate();

        // 如果加密配置有效，运行种子数据
        var seeder = scope.ServiceProvider.GetRequiredService<ProviderSeederService>();
        await seeder.SeedFromConfigurationAsync();

        logger.LogInformation("[Startup] Provider seed data initialized successfully");
    }
    catch (InvalidOperationException ex)
    {
        logger.LogCritical(ex,
            "[Startup] CRITICAL: Encryption configuration validation failed. Application cannot start securely. " +
            "Please configure 'Encryption:Key' (32 bytes) and 'Encryption:IV' (16 bytes) in appsettings.json or environment variables.");
        throw; // 终止启动，防止数据在不安全状态下写入
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "[Startup] CRITICAL: Failed to seed provider data. Application startup aborted.");
        throw;
    }
}

// 启动定时清理任务
using (var scope = app.Services.CreateScope())
{
    var jobService = scope.ServiceProvider.GetService<IBackgroundJobService>();
    if (jobService != null)
    {
        jobService.CleanupLegacyRecurringJobs();

        // 每日清理过期文件
        jobService.ScheduleDailyCleanup();

        // 每日清理不活跃会话（90天未更新）
        jobService.ScheduleSessionCleanup();

        // 每小时清理卡住的「生成中」消息
        jobService.ScheduleStuckMessagesCleanup();

        // 每日记忆沉淀扫描（凌晨4:00）
        jobService.ScheduleDailyMemoryConsolidationScan();

        // 每日系统经验修剪（凌晨5:00）
        jobService.ScheduleDailyExperiencePruning();
    }
}

// 初始化 Elasticsearch 索引
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var esOptions = scope.ServiceProvider.GetRequiredService<IOptions<ElasticsearchOptions>>().Value;

    if (esOptions.Enabled)
    {
        try
        {
            var searchService = scope.ServiceProvider.GetRequiredService<IElasticsearchSearchService>();

            // 检查 ES 是否可用
            var isAvailable = await searchService.IsAvailableAsync();
            if (isAvailable)
            {
                // 确保索引存在
                await searchService.EnsureIndicesExistAsync();
                logger.LogInformation("[Startup] Elasticsearch indices initialized successfully");
            }
            else
            {
                logger.LogWarning("[Startup] Elasticsearch service is not available. Search features will fall back to database.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Startup] Failed to initialize Elasticsearch indices. Search features will fall back to database.");
        }
    }
    else
    {
        logger.LogInformation("[Startup] Elasticsearch is disabled in configuration");
    }
}

app.MapDefaultEndpoints();

app.Run();
