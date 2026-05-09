using DevNexus.Shared.DTOs.Auth;

namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 用户管理 API 服务接口
/// </summary>
public interface IUserApiService
{
    #region 当前用户

    /// <summary>
    /// 获取当前用户信息
    /// </summary>
    Task<UserInfo> GetCurrentUserAsync();

    /// <summary>
    /// 修改密码
    /// </summary>
    Task<AuthResult> ChangePasswordAsync(ChangePasswordRequest request);

    /// <summary>
    /// 登出所有设备
    /// </summary>
    Task LogoutAllDevicesAsync();

    /// <summary>
    /// 更新个人资料
    /// </summary>
    Task<AuthResult> UpdateProfileAsync(UpdateProfileRequest request);

    #endregion

    #region 用户管理 (管理员)

    /// <summary>
    /// 获取用户列表（分页）
    /// </summary>
    Task<UserListResponse> GetUsersAsync(int page = 1, int pageSize = 20, string? search = null);

    /// <summary>
    /// 根据ID获取用户信息
    /// </summary>
    Task<UserInfo?> GetUserByIdAsync(Guid userId);

    /// <summary>
    /// 创建新用户
    /// </summary>
    Task<AuthResult> CreateUserAsync(CreateUserRequest request);

    /// <summary>
    /// 更新用户信息
    /// </summary>
    Task<AuthResult> UpdateUserAsync(Guid userId, UpdateUserRequest request);

    /// <summary>
    /// 删除用户
    /// </summary>
    Task<AuthResult> DeleteUserAsync(Guid userId);

    /// <summary>
    /// 重置用户密码
    /// </summary>
    Task<AuthResult> AdminResetPasswordAsync(Guid userId, string newPassword);

    /// <summary>
    /// 切换用户启用状态
    /// </summary>
    Task<AuthResult> ToggleUserStatusAsync(Guid userId);

    #endregion
}

