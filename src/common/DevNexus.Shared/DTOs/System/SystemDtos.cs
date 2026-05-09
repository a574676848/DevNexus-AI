using System;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 客户端版本信息 DTO
/// </summary>
/// <param name="LatestVersion">最新版本号</param>
/// <param name="DownloadUrl">下载地址</param>
/// <param name="ReleaseNotes">更新说明</param>
/// <param name="ForceUpdate">是否强制更新</param>
public record ClientVersionDto(string LatestVersion, string DownloadUrl, string ReleaseNotes, bool ForceUpdate);

/// <summary>
/// 健康状态响应 DTO
/// </summary>
public class HealthResponseDto
{
    /// <summary>
    /// 状态
    /// </summary>
    public string Status { get; set; } = "Healthy";

    /// <summary>
    /// 版本号
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// 文件上传结果 DTO
/// </summary>
public class FileUploadResultDto
{
    /// <summary>
    /// 对象键
    /// </summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>
    /// 文件 URL
    /// </summary>
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 是否已确认
    /// </summary>
    public bool Confirmed { get; set; }
}

/// <summary>
/// 存储服务信息 DTO
/// </summary>
public class StorageInfoDto
{
    /// <summary>
    /// 存储提供程序
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// 上传方式
    /// </summary>
    public string UploadMethod { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
