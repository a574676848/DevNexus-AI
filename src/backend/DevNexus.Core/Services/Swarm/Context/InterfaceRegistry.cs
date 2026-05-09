using System;
using System.Collections.Generic;

namespace DevNexus.Core.Services.Swarm.Context;

/// <summary>
/// 接口注册项 - 描述一个可调用的 API 接口
/// </summary>
public class ApiInterfaceEntry
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Verb { get; set; } = "GET";
    public string Path { get; set; } = string.Empty;
    public string? RequestType { get; set; }
    public string? ResponseType { get; set; }
}

/// <summary>
/// 接口注册表接口 - 允许 Agent 发现并准备调用系统接口
/// </summary>
public interface IInterfaceRegistry
{
    void RegisterInterface(ApiInterfaceEntry entry);
    List<ApiInterfaceEntry> GetAll();
    ApiInterfaceEntry? GetByName(string name);
}

/// <summary>
/// 内存中的接口注册表实现
/// </summary>
public class InMemoryInterfaceRegistry : IInterfaceRegistry
{
    private readonly Dictionary<string, ApiInterfaceEntry> _interfaces = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterInterface(ApiInterfaceEntry entry)
    {
        _interfaces[entry.Name] = entry;
    }

    public List<ApiInterfaceEntry> GetAll() => new(_interfaces.Values);

    public ApiInterfaceEntry? GetByName(string name)
    {
        return _interfaces.TryGetValue(name, out var entry) ? entry : null;
    }
}
