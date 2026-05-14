using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Constants;
using DevNexus.Core.Services;
using DevNexus.Core.Abstractions;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// 系统信息控制器
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class SystemController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SystemController> _logger;
    private readonly ICliSandboxValidationService _cliSandboxValidationService;

    /// <summary>
    /// 构造函数
    /// </summary>
    public SystemController(
        IConfiguration configuration,
        ILogger<SystemController> logger,
        ICliSandboxValidationService cliSandboxValidationService)
    {
        _configuration = configuration;
        _logger = logger;
        _cliSandboxValidationService = cliSandboxValidationService;
    }

    /// <summary>
    /// 获取服务器健康状态
    /// </summary>
    /// <remarks>
    /// 返回服务器的基本健康信息和版本号。
    /// </remarks>
    /// <returns>健康状态</returns>
    /// <response code="200">服务器正常运行</response>
    [HttpGet("health")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> GetHealth()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "1.0.0";
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? version;

        return Ok(new HealthResponse
        {
            Status = "healthy",
            ServerVersion = informationalVersion,
            Timestamp = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// 接收客户端日志并写入 Seq
    /// </summary>
    /// <remarks>
    /// 客户端异常日志通过此接口批量上报到服务端。
    /// 服务端使用 Serilog 将日志写入 Seq，支持结构化日志查询。
    /// </remarks>
    /// <param name="logs">客户端日志条目列表</param>
    /// <returns>上报结果</returns>
    /// <response code="200">日志接收成功</response>
    [HttpPost("client-logs")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult ReceiveClientLogs([FromBody] List<ClientLogEntryDto> logs)
    {
        if (logs == null || logs.Count == 0)
        {
            return Ok();
        }

        foreach (var log in logs)
        {
            var logLevel = ParseLogLevel(log.Level);

            _logger.Log(
                logLevel,
                "[客户端日志] {Source} | UserId={UserId} | Platform={Platform} | Version={ClientVersion} | {Message}",
                log.Source,
                log.UserId,
                log.Platform,
                log.ClientVersion,
                log.Message);

            if (!string.IsNullOrEmpty(log.Exception))
            {
                _logger.Log(
                    logLevel,
                    "[客户端异常详情] UserId={UserId} | Source={Source}\n{Exception}",
                    log.UserId,
                    log.Source,
                    log.Exception);
            }
        }

        return Ok();
    }

    /// <summary>
    /// 解析日志级别字符串
    /// </summary>
    private static LogLevel ParseLogLevel(string? level)
    {
        return level?.ToLower() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "information" or "info" => LogLevel.Information,
            "warning" or "warn" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "critical" or "fatal" => LogLevel.Critical,
            _ => LogLevel.Error
        };
    }

    /// <summary>
    /// 获取服务器配置信息（需要管理员权限）
    /// </summary>
    [HttpGet("info")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(ServerInfoResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ServerInfoResponseDto>> GetServerInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "1.0.0";

        var dbConnectionString = _configuration.GetConnectionString(ConnectionStringNames.Database);
        var dbProvider = !string.IsNullOrEmpty(dbConnectionString) ? "PostgreSQL" : "N/A";
        var dbHost = ExtractHost(dbConnectionString);

        var redisConnectionString = _configuration.GetConnectionString(ConnectionStringNames.Redis);
        var cacheProvider = !string.IsNullOrEmpty(redisConnectionString) ? "Redis" : "Memory";
        var redisHost = ExtractRedisHost(redisConnectionString);

        var esConnectionString = _configuration.GetConnectionString(ConnectionStringNames.Elasticsearch);
        var esProvider = !string.IsNullOrEmpty(esConnectionString) ? "Elasticsearch" : "Database";
        var esHost = ExtractHost(esConnectionString);

        var vectorDbConnectionString = _configuration.GetConnectionString(ConnectionStringNames.Qdrant);
        var vectorDbProvider = !string.IsNullOrEmpty(vectorDbConnectionString) ? "Qdrant" : "N/A";
        var vectorDbHost = ExtractHost(vectorDbConnectionString);

        var hangfireUrl = Request.Scheme.Equals("https") ? $"https://{Request.Host.Value}/hangfire" : $"http://{Request.Host.Value}/hangfire";
        var seqUrl = _configuration["Seq:ServerUrl"]
            ?? _configuration.GetConnectionString(ConnectionStringNames.Seq)?.Replace(":5341", ":5342")
            ?? "http://localhost:5341";

        return Ok(new ServerInfoResponseDto
        {
            Version = version,
            Environment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            MachineName = System.Environment.MachineName,
            OsVersion = System.Environment.OSVersion.ToString(),
            ProcessorCount = System.Environment.ProcessorCount,
            StartTime = DateTimeOffset.UtcNow,
            DatabaseProvider = dbProvider,
            DatabaseHost = dbHost,
            CacheProvider = cacheProvider,
            RedisHost = redisHost,
            ElasticsearchProvider = esProvider,
            ElasticsearchHost = esHost,
            VectorDbProvider = vectorDbProvider,
            VectorDbHost = vectorDbHost,
            HangfireUrl = hangfireUrl,
            SeqUrl = seqUrl,
            Features = new Dictionary<string, bool>
            {
                ["SignalR"] = true,
                ["Redis"] = !string.IsNullOrEmpty(redisConnectionString),
                ["Seq"] = !string.IsNullOrEmpty(_configuration["Seq:ServerUrl"]),
                ["AI"] = true,
                ["Hangfire"] = true,
                ["Elasticsearch"] = !string.IsNullOrEmpty(esConnectionString),
                ["VectorDb"] = !string.IsNullOrEmpty(vectorDbConnectionString)
            }
        });
    }

    /// <summary>
    /// 验证容器 sandbox 是否能通过当前 docker 默认上下文正常启动。
    /// </summary>
    [HttpPost("cli-sandbox/validate")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(CliSandboxValidationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CliSandboxValidationResultDto>> ValidateCliSandbox(CancellationToken cancellationToken)
    {
        var result = await _cliSandboxValidationService.ValidateContainerSandboxAsync(cancellationToken);
        return Ok(result);
    }

    private static string? ExtractHost(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString)) return null;

        try
        {
            if (connectionString.StartsWith("http://") || connectionString.StartsWith("https://"))
            {
                var uri = new Uri(connectionString);
                return $"{uri.Host}:{uri.Port}";
            }

            var parts = connectionString.Split(';')
                .Select(p => p.Split('='))
                .Where(p => p.Length == 2)
                .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);

            if (parts.TryGetValue("Host", out var host) || parts.TryGetValue("Server", out host))
            {
                var port = parts.TryGetValue("Port", out var p) ? p : "5432";
                var database = parts.TryGetValue("Database", out var db) ? db : "";
                return $"{host}:{port}/{database}";
            }
        }
        catch
        {
            return "已配置";
        }

        return "已配置";
    }

    private static string? ExtractRedisHost(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString)) return null;

        try
        {
            var firstPart = connectionString.Split(',')[0];
            return firstPart;
        }
        catch
        {
            return "已配置";
        }
    }
}

/// <summary>
/// 健康状态响应
/// </summary>
public record HealthResponse
{
    public string Status { get; init; } = string.Empty;
    public string ServerVersion { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
}
