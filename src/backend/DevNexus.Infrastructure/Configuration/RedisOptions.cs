namespace DevNexus.Infrastructure.Configuration;

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
