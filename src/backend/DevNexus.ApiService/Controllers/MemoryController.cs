using DevNexus.Domain.Abstractions;
using DevNexus.Core.Abstractions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// 用户记忆管理控制器
/// 管理用户画像（显性记忆）和情境记忆时间线
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[Authorize]
public class MemoryController : AuthenticatedControllerBase
{
    private readonly IUserMemoryService _memoryService;
    private readonly IAgentMemoryService _agentMemoryService;

    public MemoryController(
        IUserMemoryService memoryService,
        IAgentMemoryService agentMemoryService,
        IUserContextAccessor userContextAccessor)
        : base(userContextAccessor)
    {
        _memoryService = memoryService;
        _agentMemoryService = agentMemoryService;
    }

    /// <summary>
    /// 获取当前用户的画像事实列表
    /// </summary>
    /// <returns>用户画像事实列表</returns>
    /// <response code="200">获取成功</response>
    [HttpGet("facts")]
    [ProducesResponseType(typeof(List<UserFactDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserFacts(CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUserId();

        var facts = await _memoryService.GetAllUserFactsAsync(userId, cancellationToken);
        return Ok(facts);
    }

    /// <summary>
    /// 手动添加用户画像事实
    /// </summary>
    /// <param name="request">添加请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的事实</returns>
    /// <response code="200">添加成功</response>
    /// <response code="400">请求无效</response>
    [HttpPost("facts")]
    [ProducesResponseType(typeof(UserFactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddUserFact(
        [FromBody] AddUserFactRequest request,
        CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUserId();

        if (string.IsNullOrWhiteSpace(request.Category) || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { message = "分类和内容不能为空" });
        }

        var fact = await _memoryService.UpsertFactAsync(
            userId,
            request.Category,
            request.Content,
            sourceSessionId: null,
            cancellationToken);

        return Ok(fact);
    }

    /// <summary>
    /// 删除用户画像事实
    /// </summary>
    /// <param name="factId">事实ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    /// <response code="200">删除成功</response>
    /// <response code="404">事实不存在</response>
    [HttpDelete("facts/{factId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUserFact(Guid factId, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUserId();

        var result = await _memoryService.DeleteFactAsync(userId, factId, cancellationToken);
        if (!result)
        {
            return NotFound(new { message = "事实不存在或已被删除" });
        }

        return Ok(new { message = "删除成功" });
    }

    /// <summary>
    /// 固定/取消固定用户画像事实
    /// </summary>
    /// <param name="factId">事实ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    /// <response code="200">操作成功</response>
    /// <response code="404">事实不存在</response>
    [HttpPost("facts/{factId:guid}/toggle-pin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TogglePinFact(Guid factId, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUserId();

        var result = await _memoryService.TogglePinFactAsync(userId, factId, cancellationToken);
        if (!result)
        {
            return NotFound(new { message = "事实不存在" });
        }

        return Ok(new { message = "固定状态已切换" });
    }

    /// <summary>
    /// 获取情境记忆时间线
    /// </summary>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>情境记忆列表</returns>
    /// <response code="200">获取成功</response>
    [HttpGet("timeline")]
    [ProducesResponseType(typeof(List<EpisodicMemoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMemoryTimeline(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();

        var memories = await _memoryService.GetMemoryTimelineAsync(userId, page, pageSize, cancellationToken);
        return Ok(memories);
    }
    
    /// <summary>
    /// 搜索相关的情境记忆
    /// </summary>
    /// <param name="query">搜索关键词</param>
    /// <param name="topK">返回数量（默认20条）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>相关记忆列表</returns>
    /// <response code="200">搜索成功</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(List<EpisodicMemoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchEpisodicMemories(
        [FromQuery] string query,
        [FromQuery] int topK = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();

        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new { message = "搜索关键词不能为空" });
        }

        var memories = await _memoryService.SearchEpisodicMemoriesAsync(userId, query, topK, cancellationToken);
        return Ok(memories);
    }

    // ==============================================
    // 智能体系统经验管理 (System 1/2 Shared Memory)
    // ==============================================

    /// <summary>
    /// 获取系统经验列表 (此操作仅限管理员，此处简化)
    /// </summary>
    [HttpGet("system")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> GetSystemExperiences(
        [FromQuery] DevNexus.Shared.Enums.ExperienceType? type,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _agentMemoryService.GetSystemExperiencesAsync(type, search, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// 切换系统经验的固定状态
    /// </summary>
    [HttpPut("system/{id:guid}/pin")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> TogglePinSystemExperience(Guid id, CancellationToken cancellationToken)
    {
        var isPinned = await _agentMemoryService.TogglePinExperienceAsync(id, cancellationToken);
        if (!isPinned.HasValue) return NotFound();
        return Ok(new { isPinned = isPinned.Value });
    }

    /// <summary>
    /// 更新系统经验的效用评分
    /// </summary>
    [HttpPut("system/{id:guid}/score")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> UpdateSystemExperienceScore(Guid id, [FromBody] double score, CancellationToken cancellationToken)
    {
        var updatedScore = await _agentMemoryService.UpdateExperienceScoreAsync(id, score, cancellationToken);
        if (!updatedScore.HasValue) return NotFound();
        return Ok(new { score = updatedScore.Value });
    }

    /// <summary>
    /// 删除系统经验
    /// </summary>
    [HttpDelete("system/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> DeleteSystemExperience(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _agentMemoryService.DeleteExperienceAsync(id, cancellationToken);
        if (!deleted) return NotFound();
        return Ok();
    }
}
