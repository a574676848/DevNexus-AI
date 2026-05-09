using DevNexus.Core.Abstractions;
using DevNexus.Core.Models;
using DevNexus.Infrastructure.Models;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DevNexus.Infrastructure.Services.Auth;

/// <summary>
/// 基于 ASP.NET Identity 的用户身份访问实现。
/// </summary>
public class UserIdentityService : IUserIdentityService
{
    private readonly UserManager<InfrastructureUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;

    public UserIdentityService(UserManager<InfrastructureUser> userManager, RoleManager<IdentityRole<Guid>> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<UserIdentityModel?> FindByUsernameAsync(string username)
    {
        var user = await _userManager.FindByNameAsync(username);
        return user == null ? null : InfrastructureUserMapper.ToIdentityModel(user);
    }

    public async Task<UserIdentityModel?> FindByEmailAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        return user == null ? null : InfrastructureUserMapper.ToIdentityModel(user);
    }

    public async Task<UserIdentityModel?> FindByIdAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user == null ? null : InfrastructureUserMapper.ToIdentityModel(user);
    }

    public async Task<bool> CheckPasswordAsync(UserIdentityModel user, string password)
    {
        var entity = await RequireEntityAsync(user.Id);
        return await _userManager.CheckPasswordAsync(entity, password);
    }

    public async Task UpdateAsync(UserIdentityModel user)
    {
        var entity = await RequireEntityAsync(user.Id);
        InfrastructureUserMapper.Apply(user, entity);
        var result = await _userManager.UpdateAsync(entity);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }

    public async Task<AuthResult> CreateAsync(UserIdentityModel user, string password)
    {
        var entity = InfrastructureUserMapper.ToInfrastructureUser(user);
        var result = await _userManager.CreateAsync(entity, password);
        return ToAuthResult(result);
    }

    public async Task<AuthResult> DeleteAsync(UserIdentityModel user)
    {
        var entity = await RequireEntityAsync(user.Id);
        var result = await _userManager.DeleteAsync(entity);
        return ToAuthResult(result);
    }

    public async Task<IList<string>> GetRolesAsync(UserIdentityModel user)
    {
        var entity = await RequireEntityAsync(user.Id);
        return await _userManager.GetRolesAsync(entity);
    }

    public async Task<bool> IsInRoleAsync(UserIdentityModel user, string roleName)
    {
        var entity = await RequireEntityAsync(user.Id);
        return await _userManager.IsInRoleAsync(entity, roleName);
    }

    public Task<bool> RoleExistsAsync(string roleName)
    {
        return _roleManager.RoleExistsAsync(roleName);
    }

    public async Task CreateRoleAsync(Guid roleId, string roleName)
    {
        var role = new IdentityRole<Guid>
        {
            Id = roleId,
            Name = roleName,
            NormalizedName = roleName.ToUpperInvariant()
        };

        var result = await _roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }

    public async Task AddToRoleAsync(UserIdentityModel user, string roleName)
    {
        var entity = await RequireEntityAsync(user.Id);
        await _userManager.AddToRoleAsync(entity, roleName);
    }

    public async Task RemoveFromRolesAsync(UserIdentityModel user, IEnumerable<string> roles)
    {
        var entity = await RequireEntityAsync(user.Id);
        await _userManager.RemoveFromRolesAsync(entity, roles);
    }

    public async Task<AuthResult> ResetPasswordAsync(UserIdentityModel user, string newPassword)
    {
        var entity = await RequireEntityAsync(user.Id);
        var token = await _userManager.GeneratePasswordResetTokenAsync(entity);
        var result = await _userManager.ResetPasswordAsync(entity, token, newPassword);
        return ToAuthResult(result);
    }

    public async Task<AuthResult> ChangePasswordAsync(UserIdentityModel user, string currentPassword, string newPassword)
    {
        var entity = await RequireEntityAsync(user.Id);
        var result = await _userManager.ChangePasswordAsync(entity, currentPassword, newPassword);
        return ToAuthResult(result);
    }

    public async Task<UserListResponse> GetUsersAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = _userManager.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(u =>
                (u.UserName != null && u.UserName.ToLower().Contains(searchLower)) ||
                (u.Email != null && u.Email.ToLower().Contains(searchLower)) ||
                u.DisplayName.ToLower().Contains(searchLower));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = new List<UserListItem>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            items.Add(new UserListItem
            {
                Id = user.Id,
                Username = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl,
                IsEnabled = user.IsEnabled,
                Roles = roles.ToList(),
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt
            });
        }

        return new UserListResponse
        {
            Items = items,
            TotalCount = totalCount,
            PageSize = pageSize,
            PageNumber = page
        };
    }

    public async Task<Dictionary<Guid, UserInfo>> GetUserInfosByIdsAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, UserInfo>();
        }

        var users = await _userManager.Users
            .Where(user => ids.Contains(user.Id))
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, UserInfo>(users.Count);
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result[user.Id] = new UserInfo
            {
                Id = user.Id,
                Username = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl,
                Roles = roles.ToList()
            };
        }

        return result;
    }

    public async Task<int> CountEnabledUsersInRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        var users = await _userManager.GetUsersInRoleAsync(roleName);
        cancellationToken.ThrowIfCancellationRequested();
        return users.Count(user => user.IsEnabled);
    }

    private static AuthResult ToAuthResult(IdentityResult result)
    {
        return new AuthResult
        {
            Succeeded = result.Succeeded,
            Errors = result.Errors.Select(error => error.Description).ToList()
        };
    }

    private async Task<InfrastructureUser> RequireEntityAsync(Guid userId)
    {
        var entity = await _userManager.FindByIdAsync(userId.ToString());
        if (entity == null)
        {
            throw new InvalidOperationException($"User '{userId}' not found.");
        }

        return entity;
    }

}
