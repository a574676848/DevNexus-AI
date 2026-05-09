using DevNexus.Shared.DTOs.Auth;
using DevNexus.Shared.DTOs;
using DevNexus.Core.Models;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 身份用户访问抽象，隔离 ASP.NET Identity 的直接依赖。
/// </summary>
public interface IUserIdentityService
{
    Task<UserIdentityModel?> FindByUsernameAsync(string username);
    Task<UserIdentityModel?> FindByEmailAsync(string email);
    Task<UserIdentityModel?> FindByIdAsync(Guid userId);
    Task<bool> CheckPasswordAsync(UserIdentityModel user, string password);
    Task UpdateAsync(UserIdentityModel user);
    Task<AuthResult> CreateAsync(UserIdentityModel user, string password);
    Task<AuthResult> DeleteAsync(UserIdentityModel user);
    Task<IList<string>> GetRolesAsync(UserIdentityModel user);
    Task<bool> IsInRoleAsync(UserIdentityModel user, string roleName);
    Task<bool> RoleExistsAsync(string roleName);
    Task CreateRoleAsync(Guid roleId, string roleName);
    Task AddToRoleAsync(UserIdentityModel user, string roleName);
    Task RemoveFromRolesAsync(UserIdentityModel user, IEnumerable<string> roles);
    Task<AuthResult> ResetPasswordAsync(UserIdentityModel user, string newPassword);
    Task<AuthResult> ChangePasswordAsync(UserIdentityModel user, string currentPassword, string newPassword);
    Task<UserListResponse> GetUsersAsync(int page = 1, int pageSize = 20, string? search = null, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, UserInfo>> GetUserInfosByIdsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);
    Task<int> CountEnabledUsersInRoleAsync(string roleName, CancellationToken cancellationToken = default);
}
