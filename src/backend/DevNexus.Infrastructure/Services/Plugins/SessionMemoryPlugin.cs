using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using DevNexus.Core.Abstractions;
using DevNexus.Core.Models.Execution;
using DevNexus.Core.Services.Chat;
using Microsoft.SemanticKernel;

namespace DevNexus.Infrastructure.Services.Plugins;

/// <summary>
/// 会话临时记忆插件 — LLM 可主动创建、读取、更新会话内的结构化笔记
/// 
/// 设计要点：
/// 1. 记忆目录 (_index.md) 始终注入 System Prompt（≤2000 字符）
/// 2. 详细记忆文件按需通过工具调用读取
/// 3. 不与 ChatHistory 重叠 — 存储提炼后的关键信息
/// </summary>
[Description("管理当前会话的临时记忆笔记。可以保存项目结构、已知问题、进度等关键信息")]
public class SessionMemoryPlugin
{
    private readonly ISessionMemoryService _memoryService;
    private readonly IUserContextAccessor _userContext;
    
    public SessionMemoryPlugin(ISessionMemoryService memoryService, IUserContextAccessor userContext)
    {
        _memoryService = memoryService;
        _userContext = userContext;
    }
    
    private (string UserId, string SessionId) GetContext()
    {
        var userId = _userContext.CurrentUserId?.ToString() ?? System.Guid.Empty.ToString();
        var sessionId = _userContext.CurrentSessionId ?? "global-session";
        return (userId, sessionId);
    }
    
    [KernelFunction("save_memory")]
    [Description("保存或更新一条会话记忆，会自动更新记忆目录")]
    public async Task<string> SaveMemoryAsync(
        [Description("记忆名称（如 project-structure, known-issues, progress）")] string name,
        [Description("记忆分类（如 项目信息, 问题记录, 进度跟踪, 依赖信息）")] string category,
        [Description("一句话摘要（≤100字，将显示在记忆目录中）")] string summary,
        [Description("详细内容（Markdown 格式）")] string content,
        CancellationToken ct = default)
    {
        // ✅ 保存开始提醒
        await ThinkingContext.EmitAsync($"💾 正在保存会话记忆: {name}...");

        var (userId, sessionId) = GetContext();
        await _memoryService.SaveAsync(userId, sessionId, name, category, summary, content, ct);

        // ✅ 保存完成提醒
        await ThinkingContext.EmitAsync($"✅ 记忆已保存: {name}");

        return TaggedExecutionText.Success($"记忆 '{name}' 已保存。");
    }
    
    [KernelFunction("read_memory")]
    [Description("读取指定名称的会话记忆详细内容")]
    public async Task<string> ReadMemoryAsync(
        [Description("记忆名称")] string name,
        CancellationToken ct = default)
    {
        // ✅ 读取开始提醒
        await ThinkingContext.EmitAsync($"📖 正在读取会话记忆: {name}...");

        var (userId, sessionId) = GetContext();
        var content = await _memoryService.ReadAsync(userId, sessionId, name, ct);
        return content ?? $"[NOT_FOUND] 记忆 '{name}' 不存在。";
    }
    
    [KernelFunction("list_memories")]
    [Description("列出当前会话的所有记忆条目（仅目录级别）")]
    public async Task<string> ListMemoriesAsync(CancellationToken ct = default)
    {
        var (userId, sessionId) = GetContext();
        return await _memoryService.GetIndexAsync(userId, sessionId, ct);
    }
    
    [KernelFunction("delete_memory")]
    [Description("删除指定名称的会话记忆")]
    public async Task<string> DeleteMemoryAsync(
        [Description("记忆名称")] string name,
        CancellationToken ct = default)
    {
        // ✅ 删除开始提醒
        await ThinkingContext.EmitAsync($"🗑️ 正在删除会话记忆: {name}...");

        var (userId, sessionId) = GetContext();
        await _memoryService.DeleteAsync(userId, sessionId, name, ct);
        return TaggedExecutionText.Success($"记忆 '{name}' 已删除。");
    }
}
