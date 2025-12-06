using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Seq;
using DevNexus.Infrastructure.Configuration;
using DevNexus.Infrastructure.Models;

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
    /// 注册基础设施服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置对象</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 注册配置选项
        services.Configure<DatabaseOptions>(configuration.GetSection("Database"));
        services.Configure<RedisOptions>(configuration.GetSection("Redis"));
        services.Configure<SeqOptions>(configuration.GetSection("Seq"));
        
        // 注册数据库上下文
        services.AddDatabaseContext(configuration);
        
        // 注册 ASP.NET Identity
        services.AddIdentityServices();
        
        // 注册Redis缓存
        services.AddRedisCache(configuration);
        
        // 注册Seq日志
        services.AddSeqLogging(configuration);
        
        return services;
    }
    
    /// <summary>
    /// 获取 Redis 连接字符串
    /// </summary>
    /// <param name="configuration">配置对象</param>
    /// <returns>Redis 连接字符串，如果未启用则返回 null</returns>
    public static string? GetRedisConnectionString(this IConfiguration configuration)
    {
        var redisOptions = configuration.GetSection("Redis").Get<RedisOptions>();
        return redisOptions?.Enabled == true ? redisOptions.ConnectionString : null;
    }
    
    /// <summary>
    /// 注册数据库上下文
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置对象</param>
    private static void AddDatabaseContext(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbOptions = configuration.GetSection("Database").Get<DatabaseOptions>()
            ?? throw new ArgumentNullException(nameof(DatabaseOptions), "Database configuration is required");
        
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(dbOptions.ConnectionString);
            
            if (dbOptions.EnableDetailedLogging)
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });
        
        // 注册数据库健康检查
        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>(name: "PostgreSQL");
    }
    
    /// <summary>
    /// 注册 ASP.NET Identity 服务
    /// </summary>
    /// <param name="services">服务集合</param>
    private static void AddIdentityServices(this IServiceCollection services)
    {
        services.AddIdentity<User, IdentityRole<Guid>>(options =>
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
    /// 注册Redis缓存
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置对象</param>
    private static void AddRedisCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisOptions = configuration.GetSection("Redis").Get<RedisOptions>()
            ?? throw new ArgumentNullException(nameof(RedisOptions), "Redis configuration is required");
        
        if (redisOptions.Enabled)
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisOptions.ConnectionString;
                options.InstanceName = redisOptions.InstanceName;
            });
        }
    }
    
    /// <summary>
    /// 注册Seq日志
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置对象</param>
    private static void AddSeqLogging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var seqOptions = configuration.GetSection("Seq").Get<SeqOptions>()
            ?? throw new ArgumentNullException(nameof(SeqOptions), "Seq configuration is required");
        
        if (seqOptions.Enabled)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(Enum.Parse<LogEventLevel>(seqOptions.MinimumLevel))
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "DevNexus")
                .Enrich.WithProperty("Environment", configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development")
                .WriteTo.Console()
                .WriteTo.Seq(
                    serverUrl: seqOptions.ServerUrl,
                    apiKey: seqOptions.ApiKey,
                    controlLevelSwitch: new Serilog.Core.LoggingLevelSwitch())
                .CreateLogger();
            
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddSerilog(Log.Logger, dispose: true);
            });
        }
    }
    
    /// <summary>
    /// 应用数据库迁移（同步版本）
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    [Obsolete("Use ApplyDatabaseMigrationsAsync instead")]
    public static void ApplyDatabaseMigrations(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var dbOptions = services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        
        if (dbOptions.EnableAutoMigration)
        {
            var dbContext = services.GetRequiredService<ApplicationDbContext>();
            dbContext.Database.Migrate();
        }
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
        
        if (dbOptions.EnableAutoMigration)
        {
            var dbContext = services.GetRequiredService<ApplicationDbContext>();
            await dbContext.Database.MigrateAsync();
        }
        
        // 执行种子数据
        await serviceProvider.SeedDatabaseAsync();
    }
}
