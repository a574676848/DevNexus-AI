namespace DevNexus.Infrastructure.Configuration;

/// <summary>
/// Seq日志配置选项
/// </summary>
public class SeqOptions
{
    /// <summary>
    /// Seq服务器URL
    /// </summary>
    public string ServerUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// API密钥
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// 是否启用Seq
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// 日志最小级别
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";
}
