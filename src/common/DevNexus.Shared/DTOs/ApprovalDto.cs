using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 审批操作类型
/// </summary>
public enum ApprovalActionType
{
    /// <summary>
    /// SQL 执行审批
    /// </summary>
    SqlExecution,
    
    /// <summary>
    /// 脚本执行审批
    /// </summary>
    ScriptExecution,
    
    /// <summary>
    /// 文件操作审批
    /// </summary>
    FileOperation,
    
    /// <summary>
    /// 网络请求审批
    /// </summary>
    NetworkRequest,
    
    /// <summary>
    /// 系统命令审批
    /// </summary>
    SystemCommand
}

/// <summary>
/// 审批请求 DTO
/// </summary>
public class ApprovalRequest
{
    /// <summary>
    /// 操作ID（唯一标识此次审批）
    /// </summary>
    [JsonPropertyName("actionId")]
    public Guid ActionId { get; set; }
    
    /// <summary>
    /// 操作类型
    /// </summary>
    [JsonPropertyName("actionType")]
    public ApprovalActionType ActionType { get; set; }
    
    /// <summary>
    /// 会话ID
    /// </summary>
    [JsonPropertyName("sessionId")]
    public Guid SessionId { get; set; }
    
    /// <summary>
    /// 操作描述
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// 操作负载（具体内容，如 SQL 语句、脚本代码等）
    /// </summary>
    [JsonPropertyName("payload")]
    public string Payload { get; set; } = string.Empty;
    
    /// <summary>
    /// 额外元数据
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// 审批响应 DTO
/// </summary>
public class ApprovalResponse
{
    /// <summary>
    /// 操作ID
    /// </summary>
    [JsonPropertyName("actionId")]
    public Guid ActionId { get; set; }
    
    /// <summary>
    /// 是否批准
    /// </summary>
    [JsonPropertyName("approved")]
    public bool Approved { get; set; }
    
    /// <summary>
    /// 拒绝原因（如果被拒绝）
    /// </summary>
    [JsonPropertyName("rejectionReason")]
    public string? RejectionReason { get; set; }
    
    /// <summary>
    /// 响应时间
    /// </summary>
    [JsonPropertyName("respondedAt")]
    public DateTime RespondedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 待审批操作通知 DTO（发送给客户端）
/// </summary>
public class PendingApprovalNotification
{
    /// <summary>
    /// 操作ID
    /// </summary>
    [JsonPropertyName("actionId")]
    public Guid ActionId { get; set; }
    
    /// <summary>
    /// 操作类型
    /// </summary>
    [JsonPropertyName("actionType")]
    public ApprovalActionType ActionType { get; set; }
    
    /// <summary>
    /// 会话ID
    /// </summary>
    [JsonPropertyName("sessionId")]
    public Guid SessionId { get; set; }
    
    /// <summary>
    /// 消息ID
    /// </summary>
    [JsonPropertyName("messageId")]
    public Guid MessageId { get; set; }
    
    /// <summary>
    /// 操作描述
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// 操作负载
    /// </summary>
    [JsonPropertyName("payload")]
    public string Payload { get; set; } = string.Empty;
    
    /// <summary>
    /// 超时时间（秒）
    /// </summary>
    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 300;
    
    /// <summary>
    /// 创建时间
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
