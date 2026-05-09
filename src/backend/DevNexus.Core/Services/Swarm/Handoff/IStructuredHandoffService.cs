using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevNexus.Domain.Models.Swarm;

namespace DevNexus.Core.Services.Swarm.Handoff;

/// <summary>
/// 结构化交接服务接口
/// 负责在上下文工作包之间进行数据的 Schema 验证、自动修复和状态同步
/// </summary>
public interface IStructuredHandoffService
{
    /// <summary>
    /// 执行两个上下文工作包之间的结构化交接。
    /// </summary>
    Task<HandoffPayload> ExecuteHandoffAsync(
        ContextWorkPackage sourcePackage,
        ContextWorkPackage targetPackage,
        string rawOutput,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证数据是否符合特定的交接要求
    /// </summary>
    Task<bool> ValidatePayloadAsync(
        HandoffPayload payload,
        List<HandoffSchemaConstraint> constraints,
        CancellationToken cancellationToken = default);
}
