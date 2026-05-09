using DevNexus.Core.Abstractions;
using DevNexus.Domain.Models;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// Skill 管理控制器
/// 提供 Skill 列表查询、详情查看和热重载功能
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[Authorize]
public class SkillController : AuthenticatedControllerBase
{
    private readonly ISkillRegistry _skillRegistry;
    private readonly ISkillMatcher _skillMatcher;
    private readonly ILogger<SkillController> _logger;

    public SkillController(
        ISkillRegistry skillRegistry,
        ISkillMatcher skillMatcher,
        IUserContextAccessor userContextAccessor,
        ILogger<SkillController> logger)
        : base(userContextAccessor)
    {
        _skillRegistry = skillRegistry;
        _skillMatcher = skillMatcher;
        _logger = logger;
    }

    /// <summary>
    /// 获取当前用户可用的所有 Skill
    /// </summary>
    /// <returns>Skill 列表</returns>
    /// <response code="200">获取成功</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<SkillDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableSkills(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

        var isAdmin = IsAdmin();

        await _skillRegistry.InitializeAsync(cancellationToken);
        var skills = _skillRegistry.GetAvailableSkills(userId);

        var dtos = skills.Select(s => MapToDto(s, userId, isAdmin)).ToList();
        return Ok(dtos);
    }

    /// <summary>
    /// 获取 Skill 详细指令内容
    /// </summary>
    /// <param name="name">Skill 名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Skill 详情和指令内容</returns>
    /// <response code="200">获取成功</response>
    /// <response code="404">Skill 不存在</response>
    [HttpGet("{name}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSkillDetail(string name, CancellationToken cancellationToken)
    {
        await _skillRegistry.InitializeAsync(cancellationToken);
        var skill = _skillRegistry.GetByName(name);
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        var isAdmin = IsAdmin();

        if (skill == null)
        {
            return NotFound(new { message = $"Skill '{name}' 不存在" });
        }

        var instruction = await _skillRegistry.LoadInstructionAsync(name, cancellationToken);

        return Ok(new
        {
            Skill = MapToDto(skill, userId, isAdmin),
            Instruction = instruction
        });
    }

    /// <summary>
    /// 测试 Skill 匹配（调试用）
    /// </summary>
    /// <param name="message">测试消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>匹配结果列表</returns>
    /// <response code="200">匹配成功</response>
    [HttpGet("match")]
    [ProducesResponseType(typeof(List<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> TestMatch(
        [FromQuery] string message,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

        if (string.IsNullOrWhiteSpace(message))
        {
            return BadRequest(new { message = "测试消息不能为空" });
        }

        await _skillRegistry.InitializeAsync(cancellationToken);
        var availableSkills = _skillRegistry.GetAvailableSkills(userId);
        var matches = await _skillMatcher.MatchAsync(message, availableSkills, ct: cancellationToken);

        var result = matches.Select(m => new
        {
            SkillName = m.Skill.Name,
            m.Score,
            Method = m.Method.ToString(),
            m.Skill.Description
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// 热重载所有 Skill
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>重载结果</returns>
    /// <response code="200">重载成功</response>
    [HttpPost("reload")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReloadSkills(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[API.Skill] 触发 Skill 热重载");
        await _skillRegistry.ReloadAsync(cancellationToken);

        var skills = _skillRegistry.GetAllEnabled();
        return Ok(new
        {
            message = "Skill 热重载完成",
            count = skills.Count,
            skills = skills.Select(s => s.Name).ToList()
        });
    }

    /// <summary>
    /// 上传并导入 Skill 压缩包 (.zip)
    /// 管理员默认上传到 Shared，普通用户上传到 User
    /// </summary>
    /// <param name="file">ZIP 文件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>导入后的 Skill 信息列表</returns>
    /// <response code="200">导入成功</response>
    /// <response code="400">文件格式或内容不正确</response>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(List<SkillDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadSkill(IFormFile file, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "未选择文件或文件为空" });
        }

        if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "仅支持 .zip 格式的 Skill 压缩包" });
        }

        // 判断用户是否具有 Admin 角色，决定发布范围
        var isAdmin = IsAdmin();
        var scope = isAdmin ? Shared.Enums.SkillScope.Shared : Shared.Enums.SkillScope.User;

        _logger.LogInformation("[API.Skill] 接收到 Skill 上传 | User={UserId} IsAdmin={IsAdmin} Scope={Scope} File={FileName} Size={Size}",
            userId, isAdmin, scope, file.FileName, file.Length);

        try
        {
            using var stream = file.OpenReadStream();
            var skills = await _skillRegistry.ImportSkillArchiveAsync(stream, scope, userId, cancellationToken);

            var dtos = skills.Select(s => MapToDto(s, userId, isAdmin)).ToList();
            return Ok(dtos);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "[API.Skill] Skill 内容校验失败");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API.Skill] 处理 Skill 上传失败");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "解压或导入 Skill 失败" });
        }
    }

    /// <summary>
    /// 删除 Skill
    /// 只有 Admin 可以删除 Shared/BuiltIn，用户可以删除自己的 User Skill。
    /// </summary>
    /// <param name="name">Skill 名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除结果</returns>
    /// <response code="200">删除成功</response>
    /// <response code="404">未找到该 Skill</response>
    /// <response code="403">无权删除该 Skill</response>
    [HttpDelete("{name}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSkill(string name, CancellationToken cancellationToken)
    {
        var skill = _skillRegistry.GetByName(name);
        if (skill == null) return NotFound();

        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        var isAdmin = IsAdmin();

        var isOwner = skill.OwnerUserId == userId;
        var canManage = isAdmin || (skill.Scope == Shared.Enums.SkillScope.User && isOwner);

        if (!canManage)
        {
            _logger.LogWarning("[API.Skill] 拒绝删除 | User={UserId} Name={Name} IsAdmin={IsAdmin} Owner={Owner}", userId, name, isAdmin, skill.OwnerUserId);
            return Forbid();
        }

        try
        {
            await _skillRegistry.RemoveAsync(name, cancellationToken);
            return Ok(new { message = $"成功删除 Skill: {name}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[API.Skill] 删除 Skill 失败 | Name={Name}", name);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "删除 Skill 失败" });
        }
    }

    // ==================== 私有方法 ====================

    /// <summary>
    /// SkillMetadata → SkillDto 映射
    /// </summary>
    private static SkillDto MapToDto(SkillMetadata skill, Guid userId, bool isAdmin)
    {
        var isOwner = skill.OwnerUserId == userId;
        var canManage = isAdmin || (skill.Scope == Shared.Enums.SkillScope.User && isOwner);

        return new SkillDto
        {
            Name = skill.Name,
            Description = skill.Description,
            Type = skill.Type.ToString(),
            Scope = skill.Scope.ToString(),
            Enabled = skill.Enabled,
            Priority = skill.Priority,
            Tags = skill.Tags,
            Plugins = skill.Plugins,
            Version = skill.Version,
            Author = skill.Author,
            UpdatedAt = skill.UpdatedAt,
            HasInstruction = skill.InstructionContent != null,
            IsOwner = isOwner,
            CanManage = canManage,
            Scripts = skill.Scripts.Select(s => new SkillScriptDto
            {
                Path = s.Path,
                Runtime = s.Runtime,
                Description = s.Description
            }).ToList(),
            References = skill.References,
            Metadata = skill.Metadata ?? new()
        };
    }

}
