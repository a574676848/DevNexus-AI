using DevNexus.Core.Models.Evaluation;
using DevNexus.Core.Services.Chat;
using DevNexus.Shared.Constants;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 工具执行序列验证器测试。
/// </summary>
public sealed class ToolExecutionSequenceValidatorTests
{
    /// <summary>
    /// 有效工具记录应通过验证。
    /// </summary>
    [Fact]
    public void Validate_ShouldPass_WhenToolCallIdAndArgumentsAreValid()
    {
        var records = new[]
        {
            CreateRecord(arguments: """{"query":"DevNexus"}""")
        };

        var result = ToolExecutionSequenceValidator.Validate(records);

        result.IsValid.Should().BeTrue();
        result.Message.Should().BeNull();
    }

    /// <summary>
    /// 缺少工具调用标识时应阻断自动修复。
    /// </summary>
    [Fact]
    public void Validate_ShouldStop_WhenToolCallIdMissing()
    {
        var records = new[]
        {
            CreateRecord(toolCallId: Guid.Empty)
        };

        var result = ToolExecutionSequenceValidator.Validate(records);

        result.IsValid.Should().BeFalse();
        result.Message.Should().Be(AiOptimizationConstants.ToolValidationMessages.MissingToolCallId);
    }

    /// <summary>
    /// 重复工具调用标识时应阻断自动修复。
    /// </summary>
    [Fact]
    public void Validate_ShouldStop_WhenToolCallIdDuplicated()
    {
        var sharedCallId = Guid.NewGuid();
        var records = new[]
        {
            CreateRecord(toolCallId: sharedCallId),
            CreateRecord(toolCallId: sharedCallId)
        };

        var result = ToolExecutionSequenceValidator.Validate(records);

        result.IsValid.Should().BeFalse();
        result.Message.Should().Be(AiOptimizationConstants.ToolValidationMessages.DuplicateToolCallId);
    }

    /// <summary>
    /// 完全非法 JSON 参数应阻断自动修复。
    /// </summary>
    [Fact]
    public void Validate_ShouldStop_WhenArgumentsJsonInvalid()
    {
        var records = new[]
        {
            CreateRecord(arguments: "totally invalid {][ json")
        };

        var result = ToolExecutionSequenceValidator.Validate(records);

        result.IsValid.Should().BeFalse();
        result.Message.Should().Be(AiOptimizationConstants.ToolValidationMessages.InvalidJson);
    }

    /// <summary>
    /// 缺少闭合括号的 JSON 参数应识别为疑似截断，进入确定性小步修复。
    /// </summary>
    [Fact]
    public void Validate_ShouldTreatUnclosedJsonAsTruncatedArguments()
    {
        var records = new[]
        {
            CreateRecord(arguments: """{"path":"test.cs","startLine":10""")
        };

        var result = ToolExecutionSequenceValidator.Validate(records);

        result.IsValid.Should().BeFalse();
        result.Message.Should().Be(AiOptimizationConstants.ToolValidationMessages.TruncatedArguments);
    }

    /// <summary>
    /// Provider 混入 parameter 标签时应识别为疑似截断，不进入普通非法 JSON 阻断。
    /// </summary>
    [Fact]
    public void Validate_ShouldTreatParameterTagContaminationAsTruncatedArguments()
    {
        var records = new[]
        {
            CreateRecord(arguments: "{\"path\":\"test.cs\",\"startLine\":10</parameter>\n<parameter name=\"endLine\">20}")
        };

        var result = ToolExecutionSequenceValidator.Validate(records);

        result.IsValid.Should().BeFalse();
        result.Message.Should().Be(AiOptimizationConstants.ToolValidationMessages.TruncatedArguments);
    }

    /// <summary>
    /// 合法 JSON 但不是对象参数时应阻断自动修复。
    /// </summary>
    [Theory]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("123")]
    public void Validate_ShouldStop_WhenArgumentsJsonIsNotObject(string arguments)
    {
        var records = new[]
        {
            CreateRecord(arguments: arguments)
        };

        var result = ToolExecutionSequenceValidator.Validate(records);

        result.IsValid.Should().BeFalse();
        result.Message.Should().Be(AiOptimizationConstants.ToolValidationMessages.NonObjectArguments);
    }

    /// <summary>
    /// 失败工具调用携带空对象参数时，应识别为疑似截断。
    /// </summary>
    [Fact]
    public void Validate_ShouldStop_WhenFailedToolArgumentsAreEmptyObject()
    {
        var records = new[]
        {
            CreateRecord(arguments: "{}", success: false, failureReason: ToolFailureReason.ToolFormatError)
        };

        var result = ToolExecutionSequenceValidator.Validate(records);

        result.IsValid.Should().BeFalse();
        result.Message.Should().Be(AiOptimizationConstants.ToolValidationMessages.TruncatedArguments);
    }

    /// <summary>
    /// 成功的无参工具调用允许携带空对象参数。
    /// </summary>
    [Fact]
    public void Validate_ShouldPass_WhenSuccessfulParameterlessToolUsesEmptyObject()
    {
        var records = new[]
        {
            CreateRecord(arguments: "{}")
        };

        var result = ToolExecutionSequenceValidator.Validate(records);

        result.IsValid.Should().BeTrue();
    }

    private static ToolExecutionRecord CreateRecord(
        Guid? toolCallId = null,
        string toolName = "WebSearch.SearchAsync",
        string arguments = """{"query":"DevNexus"}""",
        bool success = true,
        ToolFailureReason failureReason = ToolFailureReason.None)
    {
        return new ToolExecutionRecord
        {
            ToolCallId = toolCallId ?? Guid.NewGuid(),
            ToolName = toolName,
            Arguments = arguments,
            Success = success,
            FailureReason = failureReason
        };
    }
}
