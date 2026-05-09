namespace DevNexus.Shared.DTOs;

/// <summary>
/// Artifact 解析状态 DTO。
/// </summary>
public class ArtifactStatusDto
{
    public string TraceId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public SmartDocument? SmartDocument { get; set; }
    public Guid? SessionId { get; set; }
}
