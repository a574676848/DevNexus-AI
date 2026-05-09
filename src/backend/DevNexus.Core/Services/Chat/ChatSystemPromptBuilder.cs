using DevNexus.Domain.Models;
using DevNexus.Shared.Constants;
using Microsoft.Extensions.Logging;
using System.Text;

namespace DevNexus.Core.Services.Chat;

public sealed class ChatSystemPromptBuilder
{
    internal sealed class ChatSystemPromptBuildResult
    {
        public string Prompt { get; set; } = string.Empty;
        public string? CriticalPrompt { get; set; }
        public int MaxContextTokens { get; set; }
        public List<SkillMatchResult>? MatchedSkills { get; set; }
    }

    private readonly ChatPromptService _chatPromptService;
    private readonly ILLMProviderManagementService _llmProviderService;
    private readonly IUserMemoryService _userMemoryService;
    private readonly ISkillRegistry _skillRegistry;
    private readonly ISkillMatcher _skillMatcher;
    private readonly ISkillRuntimePathResolver _skillRuntimePathResolver;
    private readonly IUserStoragePathService _userStoragePathService;
    private readonly ISessionMemoryService _sessionMemoryService;
    private readonly IPendingInteractionRepository _pendingInteractionRepository;
    private readonly ILogger<ChatSystemPromptBuilder> _logger;

    public ChatSystemPromptBuilder(
        ChatPromptService chatPromptService,
        ILLMProviderManagementService llmProviderService,
        IUserMemoryService userMemoryService,
        ISkillRegistry skillRegistry,
        ISkillMatcher skillMatcher,
        ISkillRuntimePathResolver skillRuntimePathResolver,
        IUserStoragePathService userStoragePathService,
        ISessionMemoryService sessionMemoryService,
        IPendingInteractionRepository pendingInteractionRepository,
        ILogger<ChatSystemPromptBuilder> logger)
    {
        _chatPromptService = chatPromptService;
        _llmProviderService = llmProviderService;
        _userMemoryService = userMemoryService;
        _skillRegistry = skillRegistry;
        _skillMatcher = skillMatcher;
        _skillRuntimePathResolver = skillRuntimePathResolver;
        _userStoragePathService = userStoragePathService;
        _sessionMemoryService = sessionMemoryService;
        _pendingInteractionRepository = pendingInteractionRepository;
        _logger = logger;
    }

    internal async Task<ChatSystemPromptBuildResult> BuildAsync(
        Guid sessionId,
        Guid userId,
        Guid? providerId,
        string? currentMessage,
        string? selectedSkillName,
        Dictionary<string, object>? requestMetadata,
        CancellationToken cancellationToken)
    {
        var maxContextTokens = await EstimateMaxContextTokensAsync(providerId, cancellationToken);

        var systemPromptBuilder = new StringBuilder(_chatPromptService.GetSystemIdentity());
        systemPromptBuilder.Append(PromptConstants.Output.BlockFormatSpec);
        systemPromptBuilder.Append(PromptConstants.Output.ToolUsageGuide);

        var userTempPath = _userStoragePathService.GetUserTempPath(userId);
        var userProjectPath = _userStoragePathService.GetUserProjectPath(userId);
        systemPromptBuilder.Append(string.Format(
            PromptConstants.System.FileSecuritySandboxPrompt,
            userTempPath,
            userProjectPath));
        var criticalSystemPrompt = string.Format(
            PromptConstants.System.FileSecurityCompactReminderPrompt,
            userTempPath,
            userProjectPath);

        await AppendMemoryContextAsync(systemPromptBuilder, userId, currentMessage, cancellationToken);
        AppendToolSelectionContext(systemPromptBuilder, requestMetadata);
        await AppendPendingInteractionContextAsync(systemPromptBuilder, requestMetadata, cancellationToken);
        var matchedSkills = await AppendSkillContextAsync(
            systemPromptBuilder,
            sessionId,
            userId,
            currentMessage,
            selectedSkillName,
            cancellationToken);

        systemPromptBuilder.AppendLine();
        systemPromptBuilder.AppendLine(PromptConstants.AgentLoop.AutonomousWorkflowPrompt);
        systemPromptBuilder.AppendLine(PromptConstants.AgentLoop.ToolUsageBestPractices);

        await AppendSessionMemoryIndexAsync(systemPromptBuilder, userId, sessionId, cancellationToken);

        return new ChatSystemPromptBuildResult
        {
            Prompt = systemPromptBuilder.ToString(),
            CriticalPrompt = criticalSystemPrompt,
            MaxContextTokens = maxContextTokens,
            MatchedSkills = matchedSkills
        };
    }

    private async Task<int> EstimateMaxContextTokensAsync(
        Guid? providerId,
        CancellationToken cancellationToken)
    {
        var maxContextTokens = 128000;

        if (!providerId.HasValue)
        {
            return maxContextTokens;
        }

        try
        {
            var provider = await _llmProviderService.GetProviderByIdAsync(providerId.Value, cancellationToken);
            maxContextTokens = _chatPromptService.EstimateContextWindow(provider);
            _logger.LogDebug(
                "[AI.Chat] Provider context window: {Tokens} tokens | ProviderId={ProviderId} Model={Model}",
                maxContextTokens,
                providerId.Value,
                provider?.ModelName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[AI.Chat] Failed to get provider info, using default context window: {Default}",
                maxContextTokens);
        }

        return maxContextTokens;
    }

    private async Task AppendMemoryContextAsync(
        StringBuilder systemPromptBuilder,
        Guid userId,
        string? currentMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            var memoryContext = await _userMemoryService.BuildMemoryContextAsync(
                userId,
                currentMessage ?? string.Empty,
                cancellationToken);

            if (!memoryContext.HasMemory)
            {
                return;
            }

            if (memoryContext.UserFacts.Count > 0)
            {
                systemPromptBuilder.AppendLine();
                systemPromptBuilder.Append(PromptConstants.Memory.UserProfileHeader);
                foreach (var fact in memoryContext.UserFacts)
                {
                    systemPromptBuilder.AppendLine($"- [{fact.Category}] {fact.Content}");
                }
            }

            if (memoryContext.EpisodicMemories.Count > 0)
            {
                systemPromptBuilder.AppendLine();
                systemPromptBuilder.Append(PromptConstants.Memory.EpisodicHeader);
                foreach (var memory in memoryContext.EpisodicMemories)
                {
                    systemPromptBuilder.AppendLine($"- ({memory.Date:yyyy-MM-dd}) {memory.Summary}");
                }
            }

            _logger.LogDebug(
                "[AI.Chat] Injected memory context | UserFacts={Facts} EpisodicMemories={Episodes}",
                memoryContext.UserFacts.Count,
                memoryContext.EpisodicMemories.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AI.Chat] Failed to build memory context, proceeding without memory");
        }
    }

    private async Task<List<SkillMatchResult>?> AppendSkillContextAsync(
        StringBuilder systemPromptBuilder,
        Guid sessionId,
        Guid userId,
        string? currentMessage,
        string? selectedSkillName,
        CancellationToken cancellationToken)
    {
        try
        {
            await _skillRegistry.InitializeAsync(cancellationToken);

            var availableSkills = _skillRegistry.GetAvailableSkills(userId);
            if (availableSkills.Count == 0)
            {
                return null;
            }

            systemPromptBuilder.AppendLine();
            systemPromptBuilder.AppendLine("\n## 可用技能");
            systemPromptBuilder.AppendLine("你具备以下专项技能，在相关场景中请主动运用：");
            foreach (var skill in availableSkills)
            {
                systemPromptBuilder.AppendLine($"- **{skill.Name}**: {skill.Description}");
            }

            List<SkillMatchResult>? matchedSkills = null;
            var explicitSkill = ResolveExplicitSkill(availableSkills, selectedSkillName);
            if (explicitSkill != null)
            {
                matchedSkills =
                [
                    new SkillMatchResult
                    {
                        Skill = explicitSkill,
                        Score = 1.0,
                        Method = SkillMatchMethod.ExplicitSelection
                    }
                ];
            }
            else if (!string.IsNullOrWhiteSpace(currentMessage))
            {
                matchedSkills = await _skillMatcher.MatchAsync(
                    currentMessage,
                    availableSkills,
                    maxResults: 3,
                    ct: cancellationToken);
            }

            if (matchedSkills?.Count > 0)
            {
                systemPromptBuilder.AppendLine();
                systemPromptBuilder.AppendLine("\n## 当前激活的技能指令");
                systemPromptBuilder.AppendLine("以下技能与当前对话高度相关，请严格遵循其指令：");

                foreach (var match in matchedSkills)
                {
                    var instruction = await _skillRegistry.LoadInstructionAsync(match.Skill.Name, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(instruction))
                    {
                        systemPromptBuilder.AppendLine($"\n### 技能: {match.Skill.Name} (匹配度: {match.Score:F2})");
                        systemPromptBuilder.AppendLine(instruction);

                        var runtimeSkillPath = _skillRuntimePathResolver.TryResolveAccessiblePath(userId, match.Skill.DirectoryPath);
                        if (!string.IsNullOrWhiteSpace(runtimeSkillPath))
                        {
                            systemPromptBuilder.AppendLine("技能脚本执行约束：");
                            systemPromptBuilder.AppendLine($"- 此技能的可执行工作目录是: {runtimeSkillPath}");
                            systemPromptBuilder.AppendLine("- 当技能文档提到 scripts/... 或 <skill-root>/scripts/... 时，必须以上面的目录作为 workingDirectory 运行，不要假设当前用户项目目录包含这些脚本。");
                            systemPromptBuilder.AppendLine("- 如果需要调用 HostService.ExecuteCommandAsync，请把 workingDirectory 设为该技能目录，再使用相对脚本路径，例如 python scripts/xxx.py。");
                            systemPromptBuilder.AppendLine("- 不要把 Skill 的源目录、仓库源目录或宿主 content-root 原样传给工具；如果看到这些路径，先改写为上面的镜像目录。");
                        }
                    }
                }

                _logger.LogDebug(
                    "[AI.Chat] 注入 Skill 上下文 | Available={Available} Matched={Matched} TopSkill={Top}",
                    availableSkills.Count,
                    matchedSkills.Count,
                    matchedSkills[0].Skill.Name);

                foreach (var auditMatch in matchedSkills)
                {
                    _logger.LogInformation(
                        "[Skill.Audit] Skill 匹配 | Source=Chat UserId={UserId} SessionId={SessionId} " +
                        "SkillName={SkillName} Scope={Scope} Type={Type} " +
                        "Score={Score:F3} Method={Method}",
                        userId,
                        sessionId,
                        auditMatch.Skill.Name,
                        auditMatch.Skill.Scope,
                        auditMatch.Skill.Type,
                        auditMatch.Score,
                        auditMatch.Method);
                }
            }

            return matchedSkills;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AI.Chat] 注入 Skill 上下文失败，继续无 Skill 模式");
            return null;
        }
    }

    private async Task AppendSessionMemoryIndexAsync(
        StringBuilder systemPromptBuilder,
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var sessionMemoryIndex = await _sessionMemoryService.GetIndexAsync(
                userId.ToString(),
                sessionId.ToString(),
                cancellationToken);
            if (string.IsNullOrEmpty(sessionMemoryIndex) || sessionMemoryIndex == "（暂无会话记忆）")
            {
                return;
            }

            systemPromptBuilder.AppendLine();
            systemPromptBuilder.AppendLine(PromptConstants.AgentLoop.SessionMemoryHeader);
            systemPromptBuilder.AppendLine(sessionMemoryIndex);
            systemPromptBuilder.AppendLine(PromptConstants.AgentLoop.SessionMemoryFooter);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AI.Chat] 获取会话临时记忆索引失败");
        }
    }

    private SkillMetadata? ResolveExplicitSkill(
        IReadOnlyList<SkillMetadata> availableSkills,
        string? selectedSkillName)
    {
        if (string.IsNullOrWhiteSpace(selectedSkillName))
        {
            return null;
        }

        var explicitSkill = availableSkills.FirstOrDefault(skill =>
            string.Equals(skill.Name, selectedSkillName, StringComparison.OrdinalIgnoreCase));

        if (explicitSkill == null)
        {
            _logger.LogWarning(
                "[AI.Chat] 显式指定的 Skill 不可用 | SkillName={SkillName}",
                selectedSkillName);
        }

        return explicitSkill;
    }

    private static void AppendToolSelectionContext(
        StringBuilder systemPromptBuilder,
        Dictionary<string, object>? requestMetadata)
    {
        if (!TryGetMetadataValue(requestMetadata, "toolId", out var toolId))
        {
            return;
        }

        systemPromptBuilder.AppendLine();
        systemPromptBuilder.AppendLine("## 当前工具面板选择");

        switch (toolId)
        {
            case "notes-management":
                systemPromptBuilder.AppendLine(
                    "用户显式选择了笔记管理工具。请优先调用 NotePlugin 完成检索、归纳、写入或整理任务，输出时要明确说明执行了哪些笔记操作。\n" +
                    "如果用户需求不完整，请先补足最少必要信息，再执行笔记写入。\n" +
                    "如果是搜索笔记，请先返回命中的关键信息摘要，再给出后续建议。");
                break;
            case "web-search":
                systemPromptBuilder.AppendLine(
                    "用户显式选择了网络搜索工具。请优先调用 WebSearchPlugin 执行联网检索，并在必要时继续读取网页正文。\n" +
                    "回答中应区分检索结论与原始来源，优先给出高可信来源与简洁结论。");
                break;
            case "deep-research":
                systemPromptBuilder.AppendLine(
                    "用户显式选择了深度研究工具。请采用多轮高级搜索 + 网页正文读取 + 知识库查询的组合流程推进。\n" +
                    "需要优先调用 WebSearchPlugin.AdvancedSearchAsync、网页读取能力和 KnowledgeBasePlugin。\n" +
                    "最终请生成一份 HTML 风格研究报告，至少包含：标题、执行摘要、关键发现、证据来源、风险与不确定性、结论与建议。\n" +
                    "如果证据不足，必须明确标记缺口与下一步研究建议。");
                break;
            case "professional-image":
                systemPromptBuilder.AppendLine(
                    "用户显式选择了专业文生图工具。请优先调用 ImageGenerationPlugin，并先将用户需求扩写为专业提示词。\n" +
                    "扩写时应覆盖主体、构图、镜头、光线、材质、色彩、风格、细节质量、画幅比例与负面约束。\n" +
                    "如果用户描述过于模糊，请先补齐关键视觉参数，再执行出图。");
                break;
        }
    }

    private static bool TryGetMetadataValue(
        Dictionary<string, object>? requestMetadata,
        string key,
        out string value)
    {
        value = string.Empty;
        if (requestMetadata == null || !requestMetadata.TryGetValue(key, out var rawValue) || rawValue == null)
        {
            return false;
        }

        value = rawValue.ToString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private async Task AppendPendingInteractionContextAsync(
        StringBuilder systemPromptBuilder,
        Dictionary<string, object>? requestMetadata,
        CancellationToken cancellationToken)
    {
        if (!TryGetMetadataValue(requestMetadata, ChatMessageMetadataKeys.PendingInteractionId, out var interactionIdRaw)
            || !Guid.TryParse(interactionIdRaw, out var interactionId))
        {
            return;
        }

        var interaction = await _pendingInteractionRepository.GetByIdAsync(interactionId, cancellationToken);
        if (interaction == null
            || interaction.Status != PendingInteractionStatus.Resolved
            || interaction.ResolutionData == null
            || interaction.ResolutionData.Count == 0)
        {
            return;
        }

        systemPromptBuilder.AppendLine();
        systemPromptBuilder.AppendLine("## 用户刚刚补充的关键信息");
        systemPromptBuilder.AppendLine("以下信息由用户在挂起交互中刚刚补充，可直接用于继续执行上一次被中断的任务：");
        systemPromptBuilder.AppendLine($"- 交互标题: {interaction.Title}");
        systemPromptBuilder.AppendLine($"- 交互说明: {interaction.Description}");
        foreach (var pair in interaction.ResolutionData)
        {
            systemPromptBuilder.AppendLine($"- {pair.Key}: {pair.Value}");
        }
        systemPromptBuilder.AppendLine("请基于以上补充信息继续推进任务，不要重复向用户索取相同内容。");
    }
}
