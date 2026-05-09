using DevNexus.Domain.Models;

namespace DevNexus.Core.Services.Chat;

internal sealed class ChatSystemPromptBuildResult
{
    public string Prompt { get; init; } = string.Empty;

    public int MaxContextTokens { get; init; } = 128000;

    public List<SkillMatchResult>? MatchedSkills { get; init; }
}