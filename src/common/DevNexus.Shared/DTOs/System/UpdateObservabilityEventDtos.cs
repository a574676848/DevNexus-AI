namespace DevNexus.Shared.DTOs;

/// <summary>
/// 客户端更新事件上报请求。
/// </summary>
public class ReportUpdateClientEventRequest
{
    public string InstallationId { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string Channel { get; set; } = "stable";
    public string CurrentVersion { get; set; } = string.Empty;
    public string TargetVersion { get; set; } = string.Empty;
    public Guid? RolloutId { get; set; }
    public Guid? ReleaseId { get; set; }
    public Guid? ArtifactId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Result { get; set; } = "success";
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}
