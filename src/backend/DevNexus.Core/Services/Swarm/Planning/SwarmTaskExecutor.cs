using DevNexus.Core.Abstractions;
using DevNexus.Core.Extensions;
using DevNexus.Core.Models.Evaluation;
using DevNexus.Core.Services.Swarm.Analysis;
using DevNexus.Core.Services.Swarm.Execution;
using DevNexus.Core.Services.Swarm.Generation;
using DevNexus.Core.Services.Swarm.Routing;
using DevNexus.Domain.Enums;
using DevNexus.Domain.Models.Swarm;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text;

namespace DevNexus.Core.Services.Swarm.Planning;

/// <summary>
/// Swarm 工作包执行器。
/// </summary>
public class SwarmTaskExecutor : ISwarmTaskExecutor
{
    private readonly IAgentFactory _agentFactory;
    private readonly IKernelService _kernelService;
    private readonly DynamicTeamAssembler _teamAssembler;
    private readonly GroupChatCoordinator _groupChatCoordinator;
    private readonly IAgentRouter _agentRouter;
    private readonly IDynamicToolSelector _toolSelector;
    private readonly IToolRegistry _toolRegistry;
    private readonly IResponseEvaluator _responseEvaluator;
    private readonly IRepairContextBuilder _repairContextBuilder;
    private readonly ISwarmEventService _eventService;
    private readonly ILogger<SwarmTaskExecutor> _logger;
    private readonly EvaluationLoopOptions _evaluationOptions;

    /// <summary>
    /// 初始化工作包执行器。
    /// </summary>
    public SwarmTaskExecutor(
        IAgentFactory agentFactory,
        IKernelService kernelService,
        DynamicTeamAssembler teamAssembler,
        GroupChatCoordinator groupChatCoordinator,
        IAgentRouter agentRouter,
        IDynamicToolSelector toolSelector,
        IToolRegistry toolRegistry,
        IResponseEvaluator responseEvaluator,
        IRepairContextBuilder repairContextBuilder,
        ISwarmEventService eventService,
        ILogger<SwarmTaskExecutor> logger)
    {
        _agentFactory = agentFactory;
        _kernelService = kernelService;
        _teamAssembler = teamAssembler;
        _groupChatCoordinator = groupChatCoordinator;
        _agentRouter = agentRouter;
        _toolSelector = toolSelector;
        _toolRegistry = toolRegistry;
        _responseEvaluator = responseEvaluator;
        _repairContextBuilder = repairContextBuilder;
        _eventService = eventService;
        _logger = logger;
        _evaluationOptions = new EvaluationLoopOptions();
    }

    /// <inheritdoc />
    public async Task<SwarmTaskExecutionResult> ExecutePackageAsync(
        ContextWorkPackage package,
        Guid providerId,
        Guid userId,
        string? extraInstruction,
        CancellationToken cancellationToken)
    {
        if (package.ExecutionStrategy == SwarmExecutionStrategy.GroupDeliberation)
        {
            return await ExecuteGroupDeliberationAsync(package, providerId, cancellationToken);
        }

        return await ExecuteAgentPackageAsync(package, providerId, userId, extraInstruction, cancellationToken);
    }

    private async Task<SwarmTaskExecutionResult> ExecuteGroupDeliberationAsync(
        ContextWorkPackage package,
        Guid providerId,
        CancellationToken cancellationToken)
    {
        var domain = ToDomainType(package.ContextType);
        var team = await _teamAssembler.AssembleTeamAsync(package.Objective, domain, providerId, cancellationToken);
        if (team.Count == 0)
        {
            team.Add(new AgentPersona
            {
                Name = "Lead",
                Role = "Facilitator",
                Description = package.Objective,
                Instructions = "Complete the package."
            });
        }

        return new SwarmTaskExecutionResult
        {
            Content = await _groupChatCoordinator.RunGroupChatAsync(
                package,
                domain,
                team,
                providerId,
                package.SessionId,
                BuildExecutionContext(package, null),
                cancellationToken),
            ExecutionKind = "GroupChat",
            ExecutorName = "GroupChatCoordinator",
            Succeeded = true,
            Metadata = new Dictionary<string, string>
            {
                ["executionStrategy"] = package.ExecutionStrategy.ToString(),
                ["packageId"] = package.Id
            }
        };
    }

    private async Task<SwarmTaskExecutionResult> ExecuteAgentPackageAsync(
        ContextWorkPackage package,
        Guid providerId,
        Guid userId,
        string? extraInstruction,
        CancellationToken cancellationToken)
    {
        var domain = ToDomainType(package.ContextType);
        var teamForRouting = await _teamAssembler.AssembleTeamAsync(package.Objective, domain, providerId, cancellationToken);
        var bestAgentPersona = await _agentRouter.RouteRequestAsync(package.Objective, teamForRouting, providerId, cancellationToken);
        var agentName = bestAgentPersona?.Name ?? package.ExecutionStrategy.ToString();
        var role = bestAgentPersona?.Role ?? "ContextWorker";

        _ = _eventService.NotifyAgentStatusChangedAsync(package.SessionId, agentName, "准备中", "正在装载执行环境...", cancellationToken);

        var allTools = _toolRegistry.GetAvailableToolNames();
        var selectedTools = await _toolSelector.SelectToolsAsync(package.Objective, allTools, providerId, cancellationToken);
        var agent = await _agentFactory.CreateAgentAsync(package.Objective, domain, selectedTools, providerId, package.SessionId, null, package.Id, cancellationToken);

        var context = BuildExecutionContext(package, extraInstruction);
        var result = await ExecuteAgentAsync(agent, package, role, package.SessionId, userId, context, cancellationToken);
        result = await ExecuteEvaluationLoopAsync(package, role, providerId, userId, domain, context, agentName, result, cancellationToken);

        return new SwarmTaskExecutionResult
        {
            Content = result,
            ExecutionKind = "LlmAgent",
            ExecutorName = agentName,
            Succeeded = true,
            Metadata = new Dictionary<string, string>
            {
                ["executionStrategy"] = package.ExecutionStrategy.ToString(),
                ["packageId"] = package.Id
            }
        };
    }

    private async Task<string> ExecuteAgentAsync(
        ChatCompletionAgent agent,
        ContextWorkPackage package,
        string role,
        string sessionId,
        Guid userId,
        string context,
        CancellationToken cancellationToken)
    {
        return await _kernelService.RunWithAuditScopeAsync(
            new ModelInvocationScopeDto
            {
                OwnerType = ModelInvocationOwnerTypes.User,
                OwnerUserId = userId,
                SessionId = Guid.TryParse(sessionId, out var parsedSessionId) ? parsedSessionId : null,
                SceneCode = ModelInvocationSceneCodes.SwarmWorkPackageExecute,
                SceneCategory = ModelInvocationSceneCategories.Swarm,
                ResourceType = ModelInvocationResourceTypes.ContextWorkPackageRecord,
                ResourceId = package.Id
            },
            async () =>
            {
                var chatService = agent.Kernel.GetRequiredService<IChatCompletionService>();
                var history = new ChatHistory(agent.Instructions ?? string.Empty);
                history.AddUserMessage(context);

                _ = _eventService.NotifyAgentStatusChangedAsync(sessionId, role, "执行中", $"正在执行：{package.Title}...", cancellationToken);
                return await chatService.GetAutoContinuedChatMessageContentAsync(history, null, agent.Kernel, _logger, $"Package-{package.Id}", 10, cancellationToken);
            });
    }

    private async Task<string> ExecuteEvaluationLoopAsync(
        ContextWorkPackage package,
        string role,
        Guid providerId,
        Guid userId,
        DomainType domain,
        string context,
        string agentName,
        string initialResult,
        CancellationToken cancellationToken)
    {
        if (!_evaluationOptions.Enabled)
        {
            return initialResult;
        }

        var currentResult = initialResult;
        var currentTokenUsage = 0;

        while (true)
        {
            _ = _eventService.NotifyAgentStatusChangedAsync(package.SessionId, "审查者", "评估中", $"正在评估 {agentName} 的执行质量...", cancellationToken);

            var evalContext = new EvaluationContext
            {
                Goal = package.Objective,
                Result = currentResult,
                ExpectedOutputSchema = package.OutputContracts.FirstOrDefault()?.Schema,
                Attempt = 0,
                Role = role,
                ProviderId = providerId
            };

            var evaluation = await _responseEvaluator.EvaluateAsync(evalContext, cancellationToken);
            currentTokenUsage += EstimateTokenUsage(evaluation);
            if (currentTokenUsage > _evaluationOptions.TokenBudget)
            {
                _logger.LogWarning("[评估] 预算超限，强制终止");
                return currentResult;
            }

            if (evaluation.Passed)
            {
                _ = _eventService.NotifyAgentStatusChangedAsync(package.SessionId, "审查者", "通过", "质量评估达标！", cancellationToken);
                return currentResult;
            }

            if (!evaluation.CanRepair)
            {
                _ = _eventService.NotifyAgentStatusChangedAsync(package.SessionId, "审查者", "完成", "修复结束（不可修复）。", cancellationToken);
                return currentResult;
            }

            _ = _eventService.NotifyAgentStatusChangedAsync(package.SessionId, agentName, "修复中", "正在进行质量修复...", cancellationToken);
            var repairContext = _repairContextBuilder.Build(evalContext, evaluation);
            var repairTools = await _toolSelector.SelectToolsAsync(package.Objective, _toolRegistry.GetAvailableToolNames(), providerId, cancellationToken);
            var repairAgent = await _agentFactory.CreateAgentAsync(package.Objective, domain, repairTools, providerId, package.SessionId, null, package.Id, cancellationToken);

            currentResult = await _kernelService.RunWithAuditScopeAsync(
                new ModelInvocationScopeDto
                {
                    OwnerType = ModelInvocationOwnerTypes.User,
                    OwnerUserId = userId,
                    SessionId = Guid.TryParse(package.SessionId, out var repairSessionId) ? repairSessionId : null,
                    SceneCode = ModelInvocationSceneCodes.SwarmWorkPackageRepair,
                    SceneCategory = ModelInvocationSceneCategories.Swarm,
                    ResourceType = ModelInvocationResourceTypes.ContextWorkPackageRecord,
                    ResourceId = package.Id
                },
                async () =>
                {
                    var repairChatService = repairAgent.Kernel.GetRequiredService<IChatCompletionService>();
                    var retryHistory = new ChatHistory(repairAgent.Instructions ?? string.Empty);
                    retryHistory.AddUserMessage(context + "\n\n" + repairContext);

                    var retryMsg = await repairChatService.GetChatMessageContentAsync(retryHistory, null, repairAgent.Kernel, cancellationToken);
                    return retryMsg.Content ?? string.Empty;
                });
        }
    }

    private static string BuildExecutionContext(ContextWorkPackage package, string? extraInstruction)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine($"Package: {package.Title}");
        stringBuilder.AppendLine($"Objective: {package.Objective}");
        stringBuilder.AppendLine($"ContextType: {package.ContextType}");
        stringBuilder.AppendLine($"ExecutionStrategy: {package.ExecutionStrategy}");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine("## TaskContext");
        stringBuilder.AppendLine(package.TaskContext);
        stringBuilder.AppendLine();
        stringBuilder.AppendLine("## StateContext");
        stringBuilder.AppendLine(package.StateContext);
        stringBuilder.AppendLine();

        if (!string.IsNullOrWhiteSpace(package.MemoryContext))
        {
            stringBuilder.AppendLine("## MemoryContext");
            stringBuilder.AppendLine(package.MemoryContext);
            stringBuilder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(package.EvidenceContext))
        {
            stringBuilder.AppendLine("## EvidenceContext");
            stringBuilder.AppendLine(package.EvidenceContext);
            stringBuilder.AppendLine();
        }

        if (package.InputContracts.Count > 0)
        {
            stringBuilder.AppendLine("## InputContracts");
            foreach (var contract in package.InputContracts)
            {
                stringBuilder.AppendLine($"- {contract.Name}: {contract.Schema}");
            }
            stringBuilder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(extraInstruction))
        {
            stringBuilder.AppendLine("## ExtraInstruction");
            stringBuilder.AppendLine(extraInstruction);
        }

        return stringBuilder.ToString();
    }

    private static DomainType ToDomainType(SwarmContextType contextType)
    {
        return contextType switch
        {
            SwarmContextType.Codebase => DomainType.Coding,
            SwarmContextType.ApiContract => DomainType.Coding,
            SwarmContextType.Data => DomainType.DataAnalysis,
            SwarmContextType.Frontend => DomainType.Creative,
            SwarmContextType.Infrastructure => DomainType.Coding,
            SwarmContextType.Evidence => DomainType.General,
            _ => DomainType.General
        };
    }

    private int EstimateTokenUsage(EvaluationResult evaluation)
    {
        var feedbackLength = evaluation.Feedback?.Length ?? 0;
        var suggestionsLength = evaluation.ImprovementSuggestions?.Sum(suggestion => suggestion.Length) ?? 0;
        return (feedbackLength + suggestionsLength) / 4;
    }
}
