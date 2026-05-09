using System;
using System.Collections.Generic;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 注册系统可用的 AI 工具/插件
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// 获取当前可用的所有工具名称列表
    /// </summary>
    List<string> GetAvailableToolNames();

    /// <summary>
    /// 判断特定工具是否可用
    /// </summary>
    bool IsToolAvailable(string toolName);
}

/// <summary>
/// 简单的硬编码工具注册表 (Phase 1)
/// </summary>
public class InMemoryToolRegistry : IToolRegistry
{
    private readonly HashSet<string> _tools = new(StringComparer.OrdinalIgnoreCase)
    {
        "HostService" 
        // 可以添加 WebSearchPlugin, etc.
    };

    public List<string> GetAvailableToolNames()
    {
        return new List<string>(_tools);
    }

    public bool IsToolAvailable(string toolName)
    {
        return _tools.Contains(toolName);
    }
}
