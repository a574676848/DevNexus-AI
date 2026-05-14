namespace DevNexus.Shared.DTOs;

/// <summary>
/// 服务器信息响应 DTO
/// </summary>
public class ServerInfoResponseDto
{
    /// <summary>
    /// 服务器版本
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 运行环境
    /// </summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>
    /// 启动时间
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// 数据库提供程序
    /// </summary>
    public string? DatabaseProvider { get; set; }

    /// <summary>
    /// 数据库连接地址（脱敏）
    /// </summary>
    public string? DatabaseHost { get; set; }

    /// <summary>
    /// 缓存提供程序
    /// </summary>
    public string? CacheProvider { get; set; }

    /// <summary>
    /// Redis 连接地址（脱敏）
    /// </summary>
    public string? RedisHost { get; set; }

    /// <summary>
    /// 机器名称
    /// </summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// 操作系统版本
    /// </summary>
    public string OsVersion { get; set; } = string.Empty;

    /// <summary>
    /// 处理器数量
    /// </summary>
    public int ProcessorCount { get; set; }

    /// <summary>
    /// Elasticsearch 提供程序
    /// </summary>
    public string? ElasticsearchProvider { get; set; }

    /// <summary>
    /// Elasticsearch 连接地址（脱敏）
    /// </summary>
    public string? ElasticsearchHost { get; set; }

    /// <summary>
    /// 向量数据库提供程序
    /// </summary>
    public string? VectorDbProvider { get; set; }

    /// <summary>
    /// 向量数据库连接地址（脱敏）
    /// </summary>
    public string? VectorDbHost { get; set; }

    /// <summary>
    /// Hangfire 面板地址
    /// </summary>
    public string? HangfireUrl { get; set; }

    /// <summary>
    /// Seq 面板地址
    /// </summary>
    public string? SeqUrl { get; set; }

    /// <summary>
    /// 功能特性
    /// </summary>
    public Dictionary<string, bool> Features { get; set; } = new();
}

