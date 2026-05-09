using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Models.Swarm;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace DevNexus.Core.Services.Swarm.Handoff;

/// <summary>
/// 结构化交接协议的默认实现
/// 支持 JSON 自动提取、Schema 校验和基于 LLM 的错误修复
/// </summary>
public class StructuredHandoffService : IStructuredHandoffService
{
    private readonly IKernelService _kernelService;
    private readonly ILogger<StructuredHandoffService> _logger;

    public StructuredHandoffService(
        IKernelService kernelService,
        ILogger<StructuredHandoffService> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HandoffPayload> ExecuteHandoffAsync(
        ContextWorkPackage sourcePackage,
        ContextWorkPackage targetPackage,
        string rawOutput,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("执行交接: {SourceId} -> {TargetId}", sourcePackage.Id, targetPackage.Id);

        var payload = new HandoffPayload
        {
            SourceTaskId = sourcePackage.Id,
            TargetTaskId = targetPackage.Id,
            DataJson = ExtractJson(rawOutput)
        };

        var expectedSchema = targetPackage.OutputContracts.FirstOrDefault()?.Schema;
        if (!string.IsNullOrEmpty(expectedSchema))
        {
            _logger.LogDebug("检测到目标任务 Schema 要求，执行验证...");
            var isValid = await ValidateAgainstSchemaAsync(payload, expectedSchema, cancellationToken);
            
            if (!isValid)
            {
                _logger.LogWarning("交接数据 Schema 验证失败，尝试执行自动修复...");
                payload = await RepairPayloadAsync(sourcePackage, targetPackage, payload, expectedSchema, cancellationToken);
            }
        }
        else
        {
            payload.IsValid = true;
            payload.SchemaType = "Schemaless";
        }

        return payload;
    }

    /// <inheritdoc />
    public Task<bool> ValidatePayloadAsync(
        HandoffPayload payload,
        List<HandoffSchemaConstraint> constraints,
        CancellationToken cancellationToken = default)
    {
        // 简化的逻辑验证
        var isValid = true;
        foreach (var constraint in constraints)
        {
            if (constraint.IsRequired && !payload.DataJson.Contains(constraint.FieldName))
            {
                payload.ValidationLogs.Add($"缺失必需字段: {constraint.FieldName}");
                isValid = false;
            }
        }
        return Task.FromResult(isValid);
    }

    /// <summary>
    /// 从文本中提取第一个有效的 JSON 对象
    /// </summary>
    private string ExtractJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "{}";

        // 尝试匹配 ```json ... ```
        var codeBlockMatch = Regex.Match(text, @"```json\s*(.*?)\s*```", RegexOptions.Singleline);
        if (codeBlockMatch.Success)
        {
            return codeBlockMatch.Groups[1].Value.Trim();
        }

        // 尝试匹配第一个 { ... }
        var bracesMatch = Regex.Match(text, @"(\{.*\})", RegexOptions.Singleline);
        if (bracesMatch.Success)
        {
            return bracesMatch.Groups[1].Value.Trim();
        }

        return "{}";
    }

    /// <summary>
    /// 使用简单方式验证 JSON (实际可集成更强大的 JSON Schema 库)
    /// </summary>
    private Task<bool> ValidateAgainstSchemaAsync(HandoffPayload payload, string schema, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload.DataJson);
            payload.IsValid = true;
            return Task.FromResult(true);
        }
        catch (JsonException ex)
        {
            payload.IsValid = false;
            payload.ValidationLogs.Add($"无效的 JSON 格式: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// 当 Schema 验证失败时，使用 LLM 尝试自动修复输出数据
    /// </summary>
    private async Task<HandoffPayload> RepairPayloadAsync(
        ContextWorkPackage sourcePackage,
        ContextWorkPackage targetPackage,
        HandoffPayload failedPayload,
        string expectedSchema,
        CancellationToken cancellationToken)
    {
        try
        {
            var prompt = $"""
                你是一个数据清理专家。之前的任务输出未通过 Schema 验证。
                
                ## 源任务背景
                {sourcePackage.Objective}
                
                ## 预期的 Schema 要求
                {expectedSchema}
                
                ## 原始错误输出
                {failedPayload.DataJson}
                
                ## 验证日志
                {string.Join("\n", failedPayload.ValidationLogs)}
                
                ## 要求
                请修复上述 JSON 数据，使其严格符合 Schema 要求。
                只需输出修复后的 JSON，不要有任何解释性文字。
                """;

            var history = new ChatHistory();
            history.AddUserMessage(prompt);

            var result = await _kernelService.GetChatCompletionAsync(
                history,
                Guid.Empty,
                cancellationToken: cancellationToken,
                enableAutoFunctionCalling: false,
                auditScope: new ModelInvocationScopeDto
                {
                    OwnerType = ModelInvocationOwnerTypes.System,
                    SceneCode = ModelInvocationSceneCodes.HandoffStructuredOutput,
                    SceneCategory = ModelInvocationSceneCategories.Swarm,
                    ResourceType = ModelInvocationResourceTypes.ContextWorkPackageRecord,
                    ResourceId = sourcePackage.Id
                });
            var repairedJson = ExtractJson(result.Content ?? "{}");

            return new HandoffPayload
            {
                SourceTaskId = sourcePackage.Id,
                TargetTaskId = targetPackage.Id,
                DataJson = repairedJson,
                IsValid = true,
                SchemaType = "AutoRepaired"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据自动修复失败，任务可能需要在降级模式下继续");
            failedPayload.ValidationLogs.Add($"修复失败: {ex.Message}");
            return failedPayload;
        }
    }
}
