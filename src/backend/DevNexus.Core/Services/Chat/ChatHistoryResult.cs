using DevNexus.Domain.Models;
using Microsoft.SemanticKernel.ChatCompletion;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// ChatHistoryService.BuildChatHistoryAsync 的返回结果
/// 同时携带构建好的 ChatHistory 和匹配到的 Skill 列表
/// </summary>
public class ChatHistoryResult
{
    /// <summary>构建完成的 ChatHistory (含 systemPrompt + L1/L2 Skill)</summary>
    public ChatHistory ChatHistory { get; set; } = null!;

    /// <summary>关键工具与沙箱约束（独立 system message，优先保留）。</summary>
    public string? CriticalSystemPrompt { get; set; }

    /// <summary>匹配到的 Skill 列表（供 KernelService 注册绑定 Plugin）</summary>
    public List<SkillMatchResult>? MatchedSkills { get; set; }
}
