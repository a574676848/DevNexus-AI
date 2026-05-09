using DevNexus.Shared.DTOs.Auth;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// Admin-oriented user management entrypoint for API/controllers.
/// </summary>
public interface IUserAdminApplicationService
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

    Task<AuthResult> ResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default);

    Task<AuthResult> ToggleUserStatusAsync(Guid userId, CancellationToken cancellationToken = default);
}
