using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// 系统信息控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SystemController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public SystemController(
        IConfiguration configuration,
        ILogger<SystemController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// 获取客户端版本信息
    /// </summary>
    /// <remarks>
    /// 返回最低支持的客户端版本和推荐版本，用于客户端检查是否需要更新。
    /// </remarks>
    /// <param name="platform">客户端平台 (ios, android, web, desktop)</param>
    /// <param name="currentVersion">当前客户端版本</param>
    /// <returns>版本检查结果</returns>
    /// <response code="200">返回版本检查结果</response>
    [HttpGet("client-version")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ClientVersionResponse), StatusCodes.Status200OK)]
    public ActionResult<ClientVersionResponse> GetClientVersion(
        [FromQuery] string platform = "web",
        [FromQuery] string? currentVersion = null)
    {
        // 从配置读取版本信息
        var minVersion = _configuration[$"ClientVersion:{platform}:MinVersion"] ?? "1.0.0";
        var recommendedVersion = _configuration[$"ClientVersion:{platform}:RecommendedVersion"] ?? "1.0.0";
        var forceUpdate = bool.Parse(_configuration[$"ClientVersion:{platform}:ForceUpdate"] ?? "false");
        var updateUrl = _configuration[$"ClientVersion:{platform}:UpdateUrl"] ?? string.Empty;
        var releaseNotes = _configuration[$"ClientVersion:{platform}:ReleaseNotes"] ?? string.Empty;

        var response = new ClientVersionResponse
        {
            Platform = platform,
            MinimumVersion = minVersion,
            RecommendedVersion = recommendedVersion,
            ForceUpdate = forceUpdate,
            UpdateUrl = updateUrl,
            ReleaseNotes = releaseNotes
        };

        // 如果提供了当前版本，计算更新状态
        if (!string.IsNullOrEmpty(currentVersion))
        {
            response.CurrentVersion = currentVersion;
            response.UpdateStatus = CalculateUpdateStatus(currentVersion, minVersion, recommendedVersion);
        }

        _logger.LogInformation(
            "[System.VersionCheck] Platform: {Platform}, CurrentVersion: {CurrentVersion}, Status: {Status}",
            platform, currentVersion ?? "not-provided", response.UpdateStatus);

        return Ok(response);
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
    /// 获取服务器配置信息（需要管理员权限）
    /// </summary>
    /// <remarks>
    /// 返回服务器的配置信息，仅管理员可访问。
    /// </remarks>
    /// <returns>服务器配置</returns>
    /// <response code="200">返回服务器配置</response>
    /// <response code="401">未授权</response>
    /// <response code="403">权限不足</response>
    [HttpGet("info")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ServerInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<ServerInfoResponse> GetServerInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "1.0.0";

        return Ok(new ServerInfoResponse
        {
            ServerVersion = version,
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            MachineName = Environment.MachineName,
            OsVersion = Environment.OSVersion.ToString(),
            ProcessorCount = Environment.ProcessorCount,
            StartTime = DateTimeOffset.UtcNow, // 实际应从服务启动时记录
            Features = new Dictionary<string, bool>
            {
                ["SignalR"] = true,
                ["Redis"] = !string.IsNullOrEmpty(_configuration.GetConnectionString("Redis")),
                ["Seq"] = !string.IsNullOrEmpty(_configuration["Seq:ServerUrl"]),
                ["AI"] = true
            }
        });
    }

    private static UpdateStatus CalculateUpdateStatus(string current, string minimum, string recommended)
    {
        if (!Version.TryParse(NormalizeVersion(current), out var currentVer))
            return UpdateStatus.Unknown;
        if (!Version.TryParse(NormalizeVersion(minimum), out var minVer))
            return UpdateStatus.Unknown;
        if (!Version.TryParse(NormalizeVersion(recommended), out var recVer))
            return UpdateStatus.Unknown;

        if (currentVer < minVer)
            return UpdateStatus.Required;
        if (currentVer < recVer)
            return UpdateStatus.Recommended;
        return UpdateStatus.UpToDate;
    }

    private static string NormalizeVersion(string version)
    {
        // 处理只有两位或三位的版本号
        var parts = version.Split('.');
        return parts.Length switch
        {
            1 => $"{parts[0]}.0.0.0",
            2 => $"{parts[0]}.{parts[1]}.0.0",
            3 => $"{parts[0]}.{parts[1]}.{parts[2]}.0",
            _ => version
        };
    }
}

/// <summary>
/// 客户端版本响应
/// </summary>
public record ClientVersionResponse
{
    /// <summary>
    /// 客户端平台
    /// </summary>
    public string Platform { get; init; } = string.Empty;

    /// <summary>
    /// 当前客户端版本（如果提供）
    /// </summary>
    public string? CurrentVersion { get; set; }

    /// <summary>
    /// 最低支持版本
    /// </summary>
    public string MinimumVersion { get; init; } = string.Empty;

    /// <summary>
    /// 推荐版本
    /// </summary>
    public string RecommendedVersion { get; init; } = string.Empty;

    /// <summary>
    /// 是否强制更新
    /// </summary>
    public bool ForceUpdate { get; init; }

    /// <summary>
    /// 更新地址
    /// </summary>
    public string UpdateUrl { get; init; } = string.Empty;

    /// <summary>
    /// 更新说明
    /// </summary>
    public string ReleaseNotes { get; init; } = string.Empty;

    /// <summary>
    /// 更新状态
    /// </summary>
    public UpdateStatus UpdateStatus { get; set; } = UpdateStatus.Unknown;
}

/// <summary>
/// 更新状态
/// </summary>
public enum UpdateStatus
{
    /// <summary>
    /// 未知
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 已是最新
    /// </summary>
    UpToDate = 1,

    /// <summary>
    /// 推荐更新
    /// </summary>
    Recommended = 2,

    /// <summary>
    /// 必须更新
    /// </summary>
    Required = 3
}

/// <summary>
/// 健康状态响应
/// </summary>
public record HealthResponse
{
    /// <summary>
    /// 健康状态
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// 服务器版本
    /// </summary>
    public string ServerVersion { get; init; } = string.Empty;

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// 服务器信息响应
/// </summary>
public record ServerInfoResponse
{
    /// <summary>
    /// 服务器版本
    /// </summary>
    public string ServerVersion { get; init; } = string.Empty;

    /// <summary>
    /// 运行环境
    /// </summary>
    public string Environment { get; init; } = string.Empty;

    /// <summary>
    /// 机器名称
    /// </summary>
    public string MachineName { get; init; } = string.Empty;

    /// <summary>
    /// 操作系统版本
    /// </summary>
    public string OsVersion { get; init; } = string.Empty;

    /// <summary>
    /// 处理器数量
    /// </summary>
    public int ProcessorCount { get; init; }

    /// <summary>
    /// 服务启动时间
    /// </summary>
    public DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// 功能特性
    /// </summary>
    public Dictionary<string, bool> Features { get; init; } = new();
}
