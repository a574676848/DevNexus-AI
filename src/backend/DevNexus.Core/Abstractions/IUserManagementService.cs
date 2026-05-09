using DevNexus.Shared.DTOs.Auth;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 用户管理服务接口。
/// </summary>
public interface IUserManagementService
{
    Task<UserListResponse> GetUsersAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<UserInfo?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<AuthResult> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    Task<AuthResult> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken = default);

    Task<AuthResult> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<AuthResult> AdminResetPasswordAsync(AdminResetPasswordRequest request, CancellationToken cancellationToken = default);

    Task<AuthResult> ToggleUserStatusAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<AuthResult> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);
}
