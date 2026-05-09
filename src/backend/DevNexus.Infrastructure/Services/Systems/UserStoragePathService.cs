using DevNexus.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Systems;

/// <summary>
/// 用户存储路径服务实现
/// 在运行目录下管理每个用户独立的 tmp 和 project 目录
/// </summary>
public class UserStoragePathService : IUserStoragePathService
{
    private readonly ILogger<UserStoragePathService> _logger;
    private readonly string _basePath;

    /// <summary>
    /// tmp 文件夹名称
    /// </summary>
    private const string TmpFolder = "tmp";

    /// <summary>
    /// project 文件夹名称
    /// </summary>
    private const string ProjectFolder = "project";

    public UserStoragePathService(ILogger<UserStoragePathService> logger)
    {
        _logger = logger;
        _basePath = AppContext.BaseDirectory;

        EnsureDirectoryExists(Path.Combine(_basePath, TmpFolder));
        EnsureDirectoryExists(Path.Combine(_basePath, ProjectFolder));

        _logger.LogInformation(
            "[UserStorage] 初始化完成 | BasePath={BasePath}",
            _basePath);
    }

    /// <inheritdoc />
    public void InitializeUserStorage(Guid userId)
    {
        var userTempPath = GetUserTempPath(userId);
        var userProjectPath = GetUserProjectPath(userId);

        if (Directory.Exists(userTempPath))
        {
            try
            {
                Directory.Delete(userTempPath, recursive: true);
                _logger.LogInformation(
                    "[UserStorage] 已清空用户临时目录 | UserId={UserId} Path={Path}",
                    userId, userTempPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[UserStorage] 清空用户临时目录失败 | UserId={UserId} Path={Path}",
                    userId, userTempPath);
            }
        }

        EnsureDirectoryExists(userTempPath);
        EnsureDirectoryExists(userProjectPath);

        _logger.LogInformation(
            "[UserStorage] 用户存储目录初始化完成 | UserId={UserId} TempPath={TempPath} ProjectPath={ProjectPath}",
            userId, userTempPath, userProjectPath);
    }

    /// <inheritdoc />
    public string GetUserTempPath(Guid userId)
    {
        return Path.GetFullPath(Path.Combine(_basePath, TmpFolder, userId.ToString()));
    }

    /// <inheritdoc />
    public string GetUserProjectPath(Guid userId)
    {
        return Path.GetFullPath(Path.Combine(_basePath, ProjectFolder, userId.ToString()));
    }

    /// <inheritdoc />
    public bool ValidateUserPathAccess(Guid userId, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            var fullPath = Path.GetFullPath(path);
            var userTempPath = GetUserTempPath(userId);
            var userProjectPath = GetUserProjectPath(userId);

            var isInTmp = fullPath.StartsWith(userTempPath, StringComparison.OrdinalIgnoreCase);
            var isInProject = fullPath.StartsWith(userProjectPath, StringComparison.OrdinalIgnoreCase);

            if (!isInTmp && !isInProject)
            {
                _logger.LogWarning(
                    "[UserStorage] 路径访问被拒绝 | UserId={UserId} Path={Path} AllowedTemp={Temp} AllowedProject={Project}",
                    userId, fullPath, userTempPath, userProjectPath);
            }

            return isInTmp || isInProject;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserStorage] 路径验证异常 | UserId={UserId} Path={Path}", userId, path);
            return false;
        }
    }

    /// <summary>
    /// 确保目录存在
    /// </summary>
    private void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            _logger.LogDebug("[UserStorage] 创建目录 | Path={Path}", path);
        }
    }
}