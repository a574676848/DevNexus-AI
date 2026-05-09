using DevNexus.Core.Models.Cli;
using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// CLI 运行时协调器。
/// 统一收口会话状态、日志分片、输入转发、终止、回滚与快照查询。
/// </summary>
public interface ICliRuntimeCoordinator : ICliProcessService
{
}
