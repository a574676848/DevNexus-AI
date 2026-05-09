namespace DevNexus.Domain.Configuration;

/// <summary>
/// 数据库配置选项
/// </summary>
public class DatabaseOptions
{
    /// <summary>
    /// 数据库连接字符串
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
    
    /// <summary>
    /// 是否启用迁移自动应用
    /// </summary>
    public bool EnableAutoMigration { get; set; } = false;
    
    /// <summary>
    /// 是否启用详细日志
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
}
