namespace DevNexus.Domain.Configuration;

/// <summary>
/// API 速率限制配置选项
/// </summary>
public class RateLimitOptions
{
    /// <summary>
    /// 是否启用速率限制
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 全局速率限制策略
    /// </summary>
    public RateLimitPolicy Global { get; set; } = new()
    {
        PermitLimit = 100,
        Window = TimeSpan.FromMinutes(1)
    };

    /// <summary>
    /// 认证用户速率限制策略
    /// </summary>
    public RateLimitPolicy Authenticated { get; set; } = new()
    {
        PermitLimit = 1000,
        Window = TimeSpan.FromMinutes(1)
    };

    /// <summary>
    /// AI 聊天接口速率限制策略
    /// </summary>
    public RateLimitPolicy Chat { get; set; } = new()
    {
        PermitLimit = 20,
        Window = TimeSpan.FromMinutes(1)
    };

    /// <summary>
    /// 代码执行接口速率限制策略
    /// </summary>
    public RateLimitPolicy CodeExecution { get; set; } = new()
    {
        PermitLimit = 10,
        Window = TimeSpan.FromMinutes(1)
    };
}

/// <summary>
/// 速率限制策略
/// </summary>
public class RateLimitPolicy
{
    /// <summary>
    /// 时间窗口内允许的请求数
    /// </summary>
    public int PermitLimit { get; set; }

    /// <summary>
    /// 时间窗口
    /// </summary>
    public TimeSpan Window { get; set; }

    /// <summary>
    /// 队列限制（可选）
    /// </summary>
    public int? QueueLimit { get; set; }
}
