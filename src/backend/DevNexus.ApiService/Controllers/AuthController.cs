using DevNexus.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// 认证控制器
/// 注意：本系统不开放注册，管理员账户通过数据库种子创建
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class AuthController : AuthenticatedControllerBase
{
    private readonly IAuthApplicationService _authApplicationService;

    /// <summary>
    /// 构造函数
    /// </summary>
    public AuthController(
        IAuthApplicationService authApplicationService,
        IUserContextAccessor userContextAccessor,
        ILogger<AuthController> logger)
        : base(userContextAccessor)
    {
        _authApplicationService = authApplicationService;
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    /// <param name="request">登录请求</param>
    /// <returns>Token 响应</returns>
    /// <response code="200">登录成功</response>
    /// <response code="401">用户名或密码错误</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var ipAddress = GetClientIpAddress();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _authApplicationService.LoginAsync(request, ipAddress, userAgent);

        if (result == null)
        {
            return Unauthorized(new { message = "用户名或密码错误" });
        }

        return Ok(result);
    }

    /// <summary>
    /// 刷新令牌
    /// </summary>
    /// <param name="request">刷新令牌请求</param>
    /// <returns>新的 Token 响应</returns>
    /// <response code="200">刷新成功</response>
    /// <response code="401">刷新令牌无效或已过期</response>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var ipAddress = GetClientIpAddress();

        var result = await _authApplicationService.RefreshTokenAsync(request, ipAddress);

        if (result == null)
        {
            return Unauthorized(new { message = "刷新令牌无效或已过期" });
        }

        return Ok(result);
    }

    /// <summary>
    /// 登出
    /// </summary>
    /// <param name="request">刷新令牌请求</param>
    /// <returns>操作结果</returns>
    /// <response code="200">登出成功</response>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        var result = await _authApplicationService.LogoutAsync(RequireCurrentUserId(), request);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(new { message = result.Message });
    }

    /// <summary>
    /// 登出所有设备
    /// </summary>
    /// <returns>操作结果</returns>
    /// <response code="200">登出成功</response>
    [HttpPost("logout-all")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> LogoutAll()
    {
        await _authApplicationService.LogoutAllDevicesAsync(RequireCurrentUserId());
        return Ok(new { message = "已从所有设备登出" });
    }

    /// <summary>
    /// 修改密码
    /// </summary>
    /// <param name="request">修改密码请求</param>
    /// <returns>操作结果</returns>
    /// <response code="200">密码修改成功</response>
    /// <response code="400">请求无效</response>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var result = await _authApplicationService.ChangePasswordAsync(RequireCurrentUserId(), request);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// 获取当前用户信息
    /// </summary>
    /// <returns>用户信息</returns>
    /// <response code="200">获取成功</response>
    /// <response code="404">用户不存在</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userInfo = await _authApplicationService.GetCurrentUserAsync(RequireCurrentUserId());

        if (userInfo == null)
        {
            return NotFound(new { message = "用户不存在" });
        }

        return Ok(userInfo);
    }

    /// <summary>
    /// 更新个人资料
    /// </summary>
    /// <param name="request">更新资料请求</param>
    /// <returns>操作结果</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">请求无效</response>
    [HttpPut("profile")]
    [Authorize]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var result = await _authApplicationService.UpdateProfileAsync(RequireCurrentUserId(), request);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// 获取客户端IP地址
    /// </summary>
    private string? GetClientIpAddress()
    {
        // 优先从 X-Forwarded-For 获取（反向代理场景）
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',').First().Trim();
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
