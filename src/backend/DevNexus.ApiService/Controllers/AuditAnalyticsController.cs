using DevNexus.Domain.Abstractions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// 审计分析 API
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class AuditAnalyticsController : AuthenticatedControllerBase
{
    private readonly IAuditAnalyticsReadService _auditAnalyticsService;
    private readonly ILogger<AuditAnalyticsController> _logger;

    public AuditAnalyticsController(
        IAuditAnalyticsReadService auditAnalyticsService,
        IUserContextAccessor userContextAccessor,
        ILogger<AuditAnalyticsController> logger)
        : base(userContextAccessor)
    {
        _auditAnalyticsService = auditAnalyticsService ?? throw new ArgumentNullException(nameof(auditAnalyticsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取当前用户的审计统计
    /// </summary>
    /// <param name="startDate">开始日期（可选）</param>
    /// <param name="endDate">结束日期（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>审计统计</returns>
    [HttpGet("my-stats")]
    [ProducesResponseType(typeof(TokenUsageStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenUsageStatsDto>> GetMyStats(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUserId();


        var stats = await _auditAnalyticsService.GetUserStatsAsync(
            userId,
            startDate,
            endDate,
            cancellationToken);


        return Ok(stats);
    }

    /// <summary>
    /// 获取当前用户的审计记录
    /// </summary>
    /// <param name="startDate">开始日期（可选）</param>
    /// <param name="endDate">结束日期（可选）</param>
    /// <param name="ownerType">调用主体类型（可选）</param>
    /// <param name="sceneCode">场景编码（可选）</param>
    /// <param name="invocationKind">调用类型（可选）</param>
    /// <param name="status">执行状态（可选）</param>
    /// <param name="pageNumber">页码（默认1）</param>
    /// <param name="pageSize">每页数量（默认50）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>审计记录分页结果</returns>
    [HttpGet("my-records")]
    [ProducesResponseType(typeof(PagedResult<TokenUsageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<TokenUsageDto>>> GetMyRecords(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? ownerType,
        [FromQuery] string? sceneCode,
        [FromQuery] string? invocationKind,
        [FromQuery] string? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();


        var result = await _auditAnalyticsService.GetUsageRecordsPagedAsync(
            userId: userId,
            startDate: startDate,
            endDate: endDate,
            ownerType: ownerType,
            sceneCode: sceneCode,
            invocationKind: invocationKind,
            status: status,
            pageNumber: pageNumber,
            pageSize: pageSize,
            cancellationToken: cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// 获取会话的审计统计
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>审计统计</returns>
    [HttpGet("session/{sessionId}")]
    [ProducesResponseType(typeof(TokenUsageStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TokenUsageStatsDto>> GetSessionStats(
        Guid sessionId,
        CancellationToken cancellationToken)
    {

        var stats = await _auditAnalyticsService.GetSessionStatsAsync(
            sessionId,
            cancellationToken);

        return Ok(stats);
    }

    /// <summary>
    /// 获取系统整体审计统计（仅管理员）
    /// </summary>
    /// <param name="startDate">开始日期（可选）</param>
    /// <param name="endDate">结束日期（可选）</param>
    /// <param name="ownerType">调用主体类型（可选）</param>
    /// <param name="sceneCode">场景编码（可选）</param>
    /// <param name="invocationKind">调用类型（可选）</param>
    /// <param name="status">执行状态（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>审计统计</returns>
    [HttpGet("system-stats")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(TokenUsageStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TokenUsageStatsDto>> GetSystemStats(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? ownerType,
        [FromQuery] string? sceneCode,
        [FromQuery] string? invocationKind,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var stats = await _auditAnalyticsService.GetSystemStatsAsync(
            startDate,
            endDate,
            ownerType,
            sceneCode,
            invocationKind,
            status,
            cancellationToken);

        return Ok(stats);
    }

    /// <summary>
    /// 获取审计中文字典。
    /// </summary>
    [HttpGet("dictionary")]
    [ProducesResponseType(typeof(AuditDictionaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuditDictionaryDto>> GetAuditDictionary(CancellationToken cancellationToken)
    {
        var dictionary = await _auditAnalyticsService.GetAuditDictionaryAsync(cancellationToken);
        return Ok(dictionary);
    }

    /// <summary>
    /// 获取审计看板数据。
    /// </summary>
    [HttpGet("dashboard")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(AuditDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuditDashboardDto>> GetAuditDashboard(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? ownerType,
        [FromQuery] string? sceneCode,
        [FromQuery] string? invocationKind,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var dashboard = await _auditAnalyticsService.GetAuditDashboardAsync(
            startDate,
            endDate,
            ownerType,
            sceneCode,
            invocationKind,
            status,
            cancellationToken);

        return Ok(dashboard);
    }

    /// <summary>
    /// 获取 AI Agent 优化看板数据。
    /// </summary>
    [HttpGet(AiOptimizationConstants.AuditAnalyticsRoutes.AiOptimizationDashboard)]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(AiOptimizationDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AiOptimizationDashboardDto>> GetAiOptimizationDashboard(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken cancellationToken)
    {
        var dashboard = await _auditAnalyticsService.GetAiOptimizationDashboardAsync(
            startDate,
            endDate,
            cancellationToken);

        return Ok(dashboard);
    }

    /// <summary>
    /// 获取所有用户的审计记录（仅管理员）
    /// </summary>
    /// <param name="userId">用户ID（可选）</param>
    /// <param name="startDate">开始日期（可选）</param>
    /// <param name="endDate">结束日期（可选）</param>
    /// <param name="pageNumber">页码（默认1）</param>
    /// <param name="pageSize">每页数量（默认50）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>审计记录列表</returns>
    [HttpGet("all-records")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(List<TokenUsageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<TokenUsageDto>>> GetAllRecords(
        [FromQuery] Guid? userId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {

        var records = await _auditAnalyticsService.GetUsageRecordsAsync(
            userId,
            startDate,
            endDate,
            pageNumber,
            pageSize,
            cancellationToken);

        return Ok(records);
    }

    /// <summary>
    /// 获取指定用户的审计统计（仅管理员）
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="startDate">开始日期（可选）</param>
    /// <param name="endDate">结束日期（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>审计统计</returns>
    [HttpGet("user/{userId}/stats")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(TokenUsageStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TokenUsageStatsDto>> GetUserStats(
        Guid userId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken cancellationToken)
    {

        var stats = await _auditAnalyticsService.GetUserStatsAsync(
            userId,
            startDate,
            endDate,
            cancellationToken);

        return Ok(stats);
    }

    /// <summary>
    /// 获取按供应商分组的统计（仅管理员）
    /// </summary>
    /// <param name="startDate">开始日期（可选）</param>
    /// <param name="endDate">结束日期（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>供应商统计列表</returns>
    [HttpGet("provider-stats")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(List<ProviderUsageStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<ProviderUsageStatsDto>>> GetProviderStats(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken cancellationToken)
    {

        var stats = await _auditAnalyticsService.GetProviderStatsAsync(
            startDate,
            endDate,
            cancellationToken);

        return Ok(stats);
    }

    /// <summary>
    /// 获取用户排行榜（仅管理员）
    /// </summary>
    /// <param name="startDate">开始日期（可选）</param>
    /// <param name="endDate">结束日期（可选）</param>
    /// <param name="topN">返回前N名（默认10）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>用户排行列表</returns>
    [HttpGet("user-ranking")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(List<UserRankingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<UserRankingDto>>> GetUserRanking(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int topN = 10,
        CancellationToken cancellationToken = default)
    {

        var ranking = await _auditAnalyticsService.GetUserRankingAsync(
            startDate,
            endDate,
            topN,
            cancellationToken);

        return Ok(ranking);
    }

    /// <summary>
    /// 获取详细的审计记录（包含用户信息，仅管理员）
    /// </summary>
    /// <param name="userId">用户ID（可选）</param>
    /// <param name="startDate">开始日期（可选）</param>
    /// <param name="endDate">结束日期（可选）</param>
    /// <param name="ownerType">调用主体类型（可选）</param>
    /// <param name="sceneCode">场景编码（可选）</param>
    /// <param name="invocationKind">调用类型（可选）</param>
    /// <param name="status">执行状态（可选）</param>
    /// <param name="pageNumber">页码（默认1）</param>
    /// <param name="pageSize">每页数量（默认50）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>详细记录列表</returns>
    [HttpGet("all-records-detailed")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(PagedResult<TokenUsageDetailedDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<TokenUsageDetailedDto>>> GetAllRecordsDetailed(
        [FromQuery] Guid? userId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? ownerType,
        [FromQuery] string? sceneCode,
        [FromQuery] string? invocationKind,
        [FromQuery] string? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {

        var records = await _auditAnalyticsService.GetDetailedUsageRecordsAsync(
            userId: userId,
            startDate: startDate,
            endDate: endDate,
            ownerType: ownerType,
            sceneCode: sceneCode,
            invocationKind: invocationKind,
            status: status,
            pageNumber: pageNumber,
            pageSize: pageSize,
            cancellationToken: cancellationToken);

        return Ok(records);
    }

}
