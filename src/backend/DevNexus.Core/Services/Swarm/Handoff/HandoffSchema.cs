using System;
using System.Collections.Generic;
using System.Text.Json;

namespace DevNexus.Core.Services.Swarm.Handoff;

/// <summary>
/// 结构化交接协议的模型定义
/// 用于在 Swarm 上下文工作包之间传递具备 Schema 约束的数据
/// </summary>
public class HandoffPayload
{
    /// <summary>
    /// 源任务 ID
    /// </summary>
    public string SourceTaskId { get; set; } = string.Empty;

    /// <summary>
    /// 目标任务 ID
    /// </summary>
    public string TargetTaskId { get; set; } = string.Empty;

    /// <summary>
    /// 结构化数据内容 (JSON)
    /// </summary>
    public string DataJson { get; set; } = "{}";

    /// <summary>
    /// 数据对应的 Schema 名称或版本
    /// </summary>
    public string SchemaType { get; set; } = "Generic";

    /// <summary>
    /// 数据验证结果日志
    /// </summary>
    public List<string> ValidationLogs { get; set; } = new();

    /// <summary>
    /// 是否通过 Schema 验证
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 元数据（如交接策略、优先级等）
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// 转换为强类型对象
    /// </summary>
    public T? GetData<T>() where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(DataJson, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 从对象序列化
    /// </summary>
    public static HandoffPayload Create<T>(string sourceId, string targetId, T data, string schemaType = "Generic")
    {
        return new HandoffPayload
        {
            SourceTaskId = sourceId,
            TargetTaskId = targetId,
            DataJson = JsonSerializer.Serialize(data),
            SchemaType = schemaType,
            IsValid = true
        };
    }
}

/// <summary>
/// 交接 Schema 约束定义
/// </summary>
public record HandoffSchemaConstraint
{
    public string FieldName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsRequired { get; init; }
    public string? ExpectedType { get; init; } // e.g., "string", "number", "boolean", "object", "array"
    public string? Format { get; init; } // e.g., "date-time", "email", "uri"
}
