using DevNexus.Core.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Skills;

/// <summary>
/// 为 Skill 脚本提供用户隔离的运行时镜像目录。
/// </summary>
public sealed class SkillRuntimePathResolver : ISkillRuntimePathResolver
{
    private readonly IUserStoragePathService _userStoragePathService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SkillRuntimePathResolver> _logger;

    private const string RuntimeSkillMirrorFolder = "skill-runtime";

    public SkillRuntimePathResolver(
        IUserStoragePathService userStoragePathService,
        IWebHostEnvironment environment,
        ILogger<SkillRuntimePathResolver> logger)
    {
        _userStoragePathService = userStoragePathService;
        _environment = environment;
        _logger = logger;
    }

    public string? TryResolveAccessiblePath(Guid userId, string requestedPath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            return null;
        }

        try
        {
            var normalizedRequestedPath = Path.GetFullPath(requestedPath);
            if (_userStoragePathService.ValidateUserPathAccess(userId, normalizedRequestedPath))
            {
                return normalizedRequestedPath;
            }

            var contentRootPath = Path.GetFullPath(_environment.ContentRootPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (!IsWithinPath(normalizedRequestedPath, contentRootPath))
            {
                return null;
            }

            var relativePath = Path.GetRelativePath(contentRootPath, normalizedRequestedPath);
            var userTempPath = _userStoragePathService.GetUserTempPath(userId);
            Directory.CreateDirectory(userTempPath);

            var mirrorRoot = Path.Combine(userTempPath, RuntimeSkillMirrorFolder, "content-root");
            EnsureSkillAssetsMirror(contentRootPath, mirrorRoot);

            var mirroredPath = string.Equals(relativePath, ".", StringComparison.OrdinalIgnoreCase)
                ? mirrorRoot
                : Path.GetFullPath(Path.Combine(mirrorRoot, relativePath));

            if (File.Exists(normalizedRequestedPath))
            {
                var parentDirectory = Path.GetDirectoryName(mirroredPath);
                if (!string.IsNullOrWhiteSpace(parentDirectory))
                {
                    Directory.CreateDirectory(parentDirectory);
                }
            }
            else
            {
                Directory.CreateDirectory(mirroredPath);
            }

            return _userStoragePathService.ValidateUserPathAccess(userId, mirroredPath)
                ? mirroredPath
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Skill.Runtime] 解析 Skill 运行时路径失败 | UserId={UserId} Requested={Requested}", userId, requestedPath);
            return null;
        }
    }

    private void EnsureSkillAssetsMirror(string contentRootPath, string mirrorRoot)
    {
        var sourceSkillsRoot = Path.Combine(contentRootPath, "wwwroot", "skills");
        if (!Directory.Exists(sourceSkillsRoot))
        {
            return;
        }

        var targetSkillsRoot = Path.Combine(mirrorRoot, "wwwroot", "skills");
        Directory.CreateDirectory(targetSkillsRoot);

        CopyDirectoryIfExists(
            Path.Combine(sourceSkillsRoot, "built-in"),
            Path.Combine(targetSkillsRoot, "built-in"));

        CopyDirectoryIfExists(
            Path.Combine(sourceSkillsRoot, "custom"),
            Path.Combine(targetSkillsRoot, "custom"));
    }

    private static void CopyDirectoryIfExists(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(sourceDir))
        {
            return;
        }

        Directory.CreateDirectory(targetDir);

        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativeDirectory = Path.GetRelativePath(sourceDir, directory);
            Directory.CreateDirectory(Path.Combine(targetDir, relativeDirectory));
        }

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativeFile = Path.GetRelativePath(sourceDir, file);
            var targetFile = Path.Combine(targetDir, relativeFile);
            var targetFileDirectory = Path.GetDirectoryName(targetFile);

            if (!string.IsNullOrWhiteSpace(targetFileDirectory))
            {
                Directory.CreateDirectory(targetFileDirectory);
            }

            if (!File.Exists(targetFile) || ShouldRefreshFile(file, targetFile))
            {
                File.Copy(file, targetFile, overwrite: true);
            }
        }
    }

    private static bool ShouldRefreshFile(string sourceFile, string targetFile)
    {
        var sourceInfo = new FileInfo(sourceFile);
        var targetInfo = new FileInfo(targetFile);

        return sourceInfo.Length != targetInfo.Length ||
               sourceInfo.LastWriteTimeUtc > targetInfo.LastWriteTimeUtc;
    }

    private static bool IsWithinPath(string candidatePath, string rootPath)
    {
        var normalizedCandidate = Path.GetFullPath(candidatePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedRoot = Path.GetFullPath(rootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return normalizedCandidate.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
               normalizedCandidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               normalizedCandidate.StartsWith(normalizedRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
