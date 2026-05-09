using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// 用户管理控制器
/// 仅限管理员访问
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class UserController : ControllerBase
{
    private readonly IUserAdminApplicationService _userAdminApplicationService;
    private readonly ILogger<UserController> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public UserController(
        IUserAdminApplicationService userAdminApplicationService,
        ILogger<UserController> logger)
    {
        _userAdminApplicationService = userAdminApplicationService;
        _logger = logger;
    }

    /// <summary>
    /// 获取用户列表（分页）
    /// </summary>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <param name="search">搜索关键字</param>
    /// <returns>用户列表</returns>
    /// <response code="200">获取成功</response>
    [HttpGet]
    [ProducesResponseType(typeof(UserListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        var result = await _userAdminApplicationService.GetUsersAsync(page, pageSize, search);
        return Ok(result);
    }

    /// <summary>
    /// 获取指定用户信息
    /// </summary>
    /// <param name="id">用户ID</param>
    /// <returns>用户信息</returns>
    /// <response code="200">获取成功</response>
    /// <response code="404">用户不存在</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        var result = await _userAdminApplicationService.GetUserByIdAsync(id);
        if (result == null)
        {
            return NotFound(new { message = "用户不存在" });
        }
        return Ok(result);
    }

    /// <summary>
    /// 创建新用户
    /// </summary>
    /// <param name="request">创建用户请求</param>
    /// <returns>操作结果</returns>
    /// <response code="200">创建成功</response>
    /// <response code="400">请求无效</response>
    [HttpPost]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new AuthResult
            {
                Succeeded = false,
                Errors = new List<string> { "用户名、邮箱和密码不能为空" }
            });
        }

        var result = await _userAdminApplicationService.CreateUserAsync(request);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// 更新用户信息
    /// </summary>
    /// <param name="id">用户ID</param>
    /// <param name="request">更新用户请求</param>
    /// <returns>操作结果</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求无效</response>
    /// <response code="404">用户不存在</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var result = await _userAdminApplicationService.UpdateUserAsync(id, request);
        if (!result.Succeeded)
        {
            if (result.Errors.Contains("用户不存在"))
            {
                return NotFound(result);
            }
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// 删除用户
    /// </summary>
    /// <param name="id">用户ID</param>
    /// <returns>操作结果</returns>
    /// <response code="200">删除成功</response>
    /// <response code="400">请求无效</response>
    /// <response code="404">用户不存在</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var result = await _userAdminApplicationService.DeleteUserAsync(id);
        if (!result.Succeeded)
        {
            if (result.Errors.Contains("用户不存在"))
            {
                return NotFound(result);
            }
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// 重置用户密码
    /// </summary>
    /// <param name="id">用户ID</param>
    /// <param name="request">新密码</param>
    /// <returns>操作结果</returns>
    /// <response code="200">重置成功</response>
    /// <response code="400">请求无效</response>
    /// <response code="404">用户不存在</response>
    [HttpPost("{id:guid}/reset-password")]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordBody request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new AuthResult
            {
                Succeeded = false,
                Errors = new List<string> { "新密码不能为空" }
            });
        }

        var result = await _userAdminApplicationService.ResetPasswordAsync(id, request.NewPassword);

        if (!result.Succeeded)
        {
            if (result.Errors.Contains("用户不存在"))
            {
                return NotFound(result);
            }
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// 切换用户启用状态
    /// </summary>
    /// <param name="id">用户ID</param>
    /// <returns>操作结果</returns>
    /// <response code="200">切换成功</response>
    /// <response code="400">请求无效</response>
    /// <response code="404">用户不存在</response>
    [HttpPut("{id:guid}/toggle-status")]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleStatus(Guid id)
    {
        var result = await _userAdminApplicationService.ToggleUserStatusAsync(id);
        if (!result.Succeeded)
        {
            if (result.Errors.Contains("用户不存在"))
            {
                return NotFound(result);
            }
            return BadRequest(result);
        }
        return Ok(result);
    }
}

/// <summary>
/// 重置密码请求体
/// </summary>
public class ResetPasswordBody
{
    /// <summary>
    /// 新密码
    /// </summary>
    public string NewPassword { get; set; } = string.Empty;
}
