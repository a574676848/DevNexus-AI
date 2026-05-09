using System;
using System.Collections.Generic;

namespace DevNexus.Core.Services.Swarm.Generation;

/// <summary>
/// 动态智能体角色配置
/// </summary>
public record AgentPersona
{
    /// <summary>
    /// 角色名称 (e.g., "PythonDataAnalyst")
    /// </summary>
    public string Role { get; init; } = "General_Assistant";

    /// <summary>
    /// 实例名称 (e.g., "Alice")
    /// </summary>
    public string Name { get; init; } = "Assistant";

    /// <summary>
    /// 简短描述
    /// </summary>
    public string Description { get; init; } = "A helpful AI assistant.";

    /// <summary>
    /// System Prompt 指令
    /// </summary>
    public string Instructions { get; init; } = "You are a helpful AI assistant.";

    /// <summary>
    /// 需要启用的工具列表
    /// </summary>
    public List<string> Tools { get; init; } = new();

    /// <summary>
    /// 温度设置 (0.0 - 1.0)
    /// </summary>
    public double Temperature { get; init; } = 0.7;
}
