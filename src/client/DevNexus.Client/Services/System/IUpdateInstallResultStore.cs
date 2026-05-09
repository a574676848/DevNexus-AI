namespace DevNexus.Client.Services.System;

/// <summary>
/// 更新安装结果存储。
/// </summary>
public interface IUpdateInstallResultStore
{
    /// <summary>
    /// 读取安装结果。
    /// </summary>
    Task<UpdateInstallResult?> GetAsync();

    /// <summary>
    /// 清空安装结果。
    /// </summary>
    Task ClearAsync();
}

/// <summary>
/// 更新安装结果。
/// </summary>
public class UpdateInstallResult
{
    public string Result { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CompletedAtUtc { get; set; }
}
