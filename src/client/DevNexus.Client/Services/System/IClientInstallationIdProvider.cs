namespace DevNexus.Client.Services.System;

/// <summary>
/// 客户端安装标识提供器。
/// </summary>
public interface IClientInstallationIdProvider
{
    /// <summary>
    /// 获取稳定安装标识。
    /// </summary>
    Task<string> GetInstallationIdAsync();
}
