// using DevNexus.Domain.Abstractions via GlobalUsings
// using DevNexus.Domain.Entities via GlobalUsings
using DevNexus.Core.Models;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs.Auth;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services;

/// <summary>
/// 用户管理服务实现
/// </summary>
public class UserManagementService : IUserManagementService
{
    private readonly IUserIdentityService _userIdentityService;
    private readonly ILogger<UserManagementService> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public UserManagementService(
        IUserIdentityService userIdentityService,
        ILogger<UserManagementService> logger)
    {
        _userIdentityService = userIdentityService;
        _logger = logger;
    }

    #region 用户管理 (Admin)

    /// <inheritdoc />
    public async Task<UserListResponse> GetUsersAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "[UserManagement.GetUsers] Fetching users | Page={Page} PageSize={PageSize} Search={Search}",
            page, pageSize, search);

        return await _userIdentityService.GetUsersAsync(page, pageSize, search, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UserInfo?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userIdentityService.FindByIdAsync(userId);
        if (user == null)
        {
            return null;
        }

        var roles = await _userIdentityService.GetRolesAsync(user);
        return new UserInfo
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            Roles = roles.ToList()
        };
    }

    /// <inheritdoc />
    public async Task<AuthResult> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "[UserManagement.CreateUser] Creating user | Username={Username} Email={Email}",
            request.Username, request.Email);

        // 检查用户名是否已存在
        var existingUser = await _userIdentityService.FindByUsernameAsync(request.Username);
        if (existingUser != null)
        {
            _logger.LogWarning("[UserManagement.CreateUser] Username already exists | Username={Username}", request.Username);
            return new AuthResult
            {
                Succeeded = false,
                Errors = new List<string> { "用户名已存在" }
            };
        }

        // 检查邮箱是否已存在
        existingUser = await _userIdentityService.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            _logger.LogWarning("[UserManagement.CreateUser] Email already exists | Email={Email}", request.Email);
            return new AuthResult
            {
                Succeeded = false,
                Errors = new List<string> { "邮箱已存在" }
            };
        }

        // 创建用户
        var user = new UserIdentityModel
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            EmailConfirmed = true,
            DisplayName = request.DisplayName ?? request.Username,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _userIdentityService.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.ToList();
            _logger.LogWarning("[UserManagement.CreateUser] Failed to create user | Errors={Errors}", string.Join(", ", errors));
            return new AuthResult
            {
                Succeeded = false,
                Errors = errors
            };
        }

        // 分配角色
        if (request.Roles != null && request.Roles.Count > 0)
        {
            foreach (var role in request.Roles)
            {
                if (await _userIdentityService.RoleExistsAsync(role))
                {
                    await _userIdentityService.AddToRoleAsync(user, role);
                }
            }
        }
        else
        {
            // 默认分配 User 角色
            if (await _userIdentityService.RoleExistsAsync(RoleNames.User))
            {
                await _userIdentityService.AddToRoleAsync(user, RoleNames.User);
            }
        }

        _logger.LogInformation("[UserManagement.CreateUser] User created successfully | UserId={UserId}", user.Id);
        return new AuthResult
        {
            Succeeded = true,
            Message = "用户创建成功"
        };
    }

    /// <inheritdoc />
    public async Task<AuthResult> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[UserManagement.UpdateUser] Updating user | UserId={UserId}", userId);

        var user = await _userIdentityService.FindByIdAsync(userId);
        if (user == null)
        {
            return new AuthResult
            {
                Succeeded = false,
                Errors = new List<string> { "用户不存在" }
            };
        }

        // 检查防止禁用最后一个管理员
        if (request.IsEnabled.HasValue && !request.IsEnabled.Value && user.IsEnabled)
        {
            var isAdmin = await _userIdentityService.IsInRoleAsync(user, RoleNames.Admin);
            if (isAdmin && await GetEnabledAdminCountAsync(cancellationToken) <= 1)
            {
                return new AuthResult
                {
                    Succeeded = false,
                    Errors = new List<string> { "不能禁用最后一个管理员账户" }
                };
            }
        }

        // 检查防止移除最后一个管理员的角色
        if (request.Roles != null)
        {
            var isAdmin = await _userIdentityService.IsInRoleAsync(user, RoleNames.Admin);
            if (isAdmin && !request.Roles.Contains(RoleNames.Admin) && user.IsEnabled)
            {
                 if (await GetEnabledAdminCountAsync(cancellationToken) <= 1)
                 {
                     return new AuthResult
                     {
                         Succeeded = false,
                         Errors = new List<string> { "不能移除最后一个管理员的管理员权限" }
                     };
                 }
            }
        }

        // 更新邮箱
        if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != user.Email)
        {
            var existingUser = await _userIdentityService.FindByEmailAsync(request.Email);
            if (existingUser != null && existingUser.Id != userId)
            {
                return new AuthResult
                {
                    Succeeded = false,
                    Errors = new List<string> { "邮箱已被其他用户使用" }
                };
            }
            user.Email = request.Email;
        }

        // 更新显示名称
        if (request.DisplayName != null)
        {
            user.DisplayName = request.DisplayName;
        }

        // 更新启用状态
        if (request.IsEnabled.HasValue)
        {
            user.IsEnabled = request.IsEnabled.Value;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _userIdentityService.UpdateAsync(user);

        // 更新角色
        if (request.Roles != null)
        {
            var currentRoles = await _userIdentityService.GetRolesAsync(user);
            await _userIdentityService.RemoveFromRolesAsync(user, currentRoles);

            foreach (var role in request.Roles)
            {
                if (await _userIdentityService.RoleExistsAsync(role))
                {
                    await _userIdentityService.AddToRoleAsync(user, role);
                }
            }
        }

        _logger.LogInformation("[UserManagement.UpdateUser] User updated successfully | UserId={UserId}", userId);
        return new AuthResult
        {
            Succeeded = true,
            Message = "用户信息更新成功"
        };
    }

    /// <inheritdoc />
    public async Task<AuthResult> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[UserManagement.DeleteUser] Deleting user | UserId={UserId}", userId);

        var user = await _userIdentityService.FindByIdAsync(userId);
        if (user == null)
        {
            return new AuthResult
            {
                Succeeded = false,
                Errors = new List<string> { "用户不存在" }
            };
        }

        // 检查是否是最后一个管理员
        var isAdmin = await _userIdentityService.IsInRoleAsync(user, RoleNames.Admin);
        if (isAdmin)
        {
            // 如果是管理员，即使它是被禁用的，我们也应该小心，但严格来说防止锁定的是"有效"管理员
            // 这里我们假设如果删除了一个被禁用的管理员，那无所谓。
            // 只有当我们要删除一个也就是"有效"的管理员时，才需要检查是否是最后一个。
            if (user.IsEnabled && await GetEnabledAdminCountAsync(cancellationToken) <= 1)
            {
                _logger.LogWarning("[UserManagement.DeleteUser] Cannot delete the last active admin | UserId={UserId}", userId);
                return new AuthResult
                {
                    Succeeded = false,
                    Errors = new List<string> { "不能删除最后一个有效的管理员账户" }
                };
            }
        }

        var result = await _userIdentityService.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return new AuthResult
            {
                Succeeded = false,
                Errors = result.Errors.ToList()
            };
        }

        _logger.LogInformation("[UserManagement.DeleteUser] User deleted successfully | UserId={UserId}", userId);
        return new AuthResult
        {
            Succeeded = true,
            Message = "用户删除成功"
        };
    }

    /// <inheritdoc />
    public async Task<AuthResult> AdminResetPasswordAsync(AdminResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[UserManagement.AdminResetPassword] Resetting password | UserId={UserId}", request.UserId);

        var user = await _userIdentityService.FindByIdAsync(request.UserId);
        if (user == null)
        {
            return new AuthResult
            {
                Succeeded = false,
                Errors = new List<string> { "用户不存在" }
            };
        }

        var result = await _userIdentityService.ResetPasswordAsync(user, request.NewPassword);

        if (!result.Succeeded)
        {
            _logger.LogWarning("[UserManagement.AdminResetPassword] Failed to reset password | Errors={Errors}", string.Join(", ", result.Errors));
            return new AuthResult
            {
                Succeeded = false,
                Errors = result.Errors.ToList()
            };
        }

        _logger.LogInformation("[UserManagement.AdminResetPassword] Password reset successfully | UserId={UserId}", request.UserId);
        return new AuthResult
        {
            Succeeded = true,
            Message = "密码重置成功"
        };
    }

    /// <inheritdoc />
    public async Task<AuthResult> ToggleUserStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[UserManagement.ToggleStatus] Toggling user status | UserId={UserId}", userId);

        var user = await _userIdentityService.FindByIdAsync(userId);
        if (user == null)
        {
            return new AuthResult
            {
                Succeeded = false,
                Errors = new List<string> { "用户不存在" }
            };
        }

        // 检查是否是管理员且要禁用
        if (user.IsEnabled)
        {
            var isAdmin = await _userIdentityService.IsInRoleAsync(user, RoleNames.Admin);
            if (isAdmin && await GetEnabledAdminCountAsync(cancellationToken) <= 1)
            {
                return new AuthResult
                {
                    Succeeded = false,
                    Errors = new List<string> { "不能禁用最后一个管理员账户" }
                };
            }
        }

        user.IsEnabled = !user.IsEnabled;
        user.UpdatedAt = DateTime.UtcNow;

        await _userIdentityService.UpdateAsync(user);

        var status = user.IsEnabled ? "启用" : "禁用";
        _logger.LogInformation("[UserManagement.ToggleStatus] User status toggled | UserId={UserId} Status={Status}", userId, status);
        return new AuthResult
        {
            Succeeded = true,
            Message = $"用户已{status}"
        };
    }

    #endregion

    #region 个人资料 (自己)

    /// <inheritdoc />
    public async Task<AuthResult> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[UserManagement.UpdateProfile] Updating profile | UserId={UserId}", userId);

        var user = await _userIdentityService.FindByIdAsync(userId);
        if (user == null)
        {
            return new AuthResult
            {
                Succeeded = false,
                Errors = new List<string> { "用户不存在" }
            };
        }

        // 验证并更新 Email
        if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != user.Email)
        {
            var existingUser = await _userIdentityService.FindByEmailAsync(request.Email);
            if (existingUser != null && existingUser.Id != userId)
            {
                return new AuthResult
                {
                    Succeeded = false,
                    Errors = new List<string> { "邮箱已被其他用户使用" }
                };
            }
            user.Email = request.Email;
        }

        // 更新手机号
        if (request.PhoneNumber != null)
        {
            user.PhoneNumber = request.PhoneNumber;
        }

        if (request.DisplayName != null)
        {
            user.DisplayName = request.DisplayName;
        }

        if (request.AvatarUrl != null)
        {
            user.AvatarUrl = request.AvatarUrl;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _userIdentityService.UpdateAsync(user);

        _logger.LogInformation("[UserManagement.UpdateProfile] Profile updated successfully | UserId={UserId}", userId);
        return new AuthResult
        {
            Succeeded = true,
            Message = "个人资料更新成功"
        };
    }

    #endregion
    
    #region 私有辅助方法

    /// <summary>
    /// 获取启用的管理员数量
    /// </summary>
    private async Task<int> GetEnabledAdminCountAsync(CancellationToken cancellationToken)
    {
        return await _userIdentityService.CountEnabledUsersInRoleAsync(RoleNames.Admin, cancellationToken);
    }

    #endregion
}
