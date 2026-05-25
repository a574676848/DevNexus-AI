namespace DevNexus.Core.Abstractions;

/// <summary>
/// 用户存储路径服务接口
/// 管理每个用户独立的临时目录和项目目录
/// </summary>
public interface IUserStoragePathService
{
    /// <summary>
    /// 初始化用户存储目录（登录时调用）
    /// 创建 tmp/{userId} 和 project/{userId} 目录。
    /// </summary>
    /// <param name="userId">用户ID</param>
    void InitializeUserStorage(Guid userId);

    /// <summary>
    /// 获取用户临时文件夹的绝对路径
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>临时文件夹绝对路径</returns>
    string GetUserTempPath(Guid userId);

    /// <summary>
    /// 获取用户项目文件夹的绝对路径
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>项目文件夹绝对路径</returns>
    string GetUserProjectPath(Guid userId);

    /// <summary>
    /// 验证指定路径是否在用户允许的存储范围内（tmp 或 project）
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="path">需要验证的路径</param>
    /// <returns>是否允许访问</returns>
    bool ValidateUserPathAccess(Guid userId, string path);
}