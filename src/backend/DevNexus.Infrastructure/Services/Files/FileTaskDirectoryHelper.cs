using System.Text.Json;
using DevNexus.Core.Abstractions;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevNexus.Infrastructure.Services.Files;

internal static class FileTaskDirectoryHelper
{
    public static string PrepareTaskDirectory(
        IUserStoragePathService userStoragePathService,
        Guid userId,
        Guid fileTaskId)
    {
        var userProjectPath = userStoragePathService.GetUserProjectPath(userId);
        var taskDirectoryPath = Path.Combine(userProjectPath, "file-tasks", fileTaskId.ToString("N"));

        Directory.CreateDirectory(taskDirectoryPath);
        Directory.CreateDirectory(Path.Combine(taskDirectoryPath, "inputs"));
        Directory.CreateDirectory(Path.Combine(taskDirectoryPath, "templates"));
        Directory.CreateDirectory(Path.Combine(taskDirectoryPath, "outputs"));

        return taskDirectoryPath;
    }

    public static async Task<List<object>> StageAssetsAsync(
        ApplicationDbContext dbContext,
        IReadOnlyCollection<Guid> assetIds,
        string taskDirectoryPath,
        string folderName,
        IFileStorageService storageService,
        CancellationToken cancellationToken)
    {
        var stagedFiles = new List<object>();
        var targetFolder = Path.Combine(taskDirectoryPath, folderName);

        var assets = await dbContext.FileAssets
            .AsNoTracking()
            .Where(asset => assetIds.Contains(asset.Id) && !asset.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var assetId in assetIds)
        {
            var asset = assets.FirstOrDefault(x => x.Id == assetId);
            if (asset == null)
            {
                continue;
            }

            var targetFilePath = BuildUniqueTargetPath(targetFolder, asset.OriginalFileName);

            await using var sourceStream = await storageService.DownloadFileAsync(asset.FileUrl);
            await using var targetStream = File.Create(targetFilePath);
            await sourceStream.CopyToAsync(targetStream, cancellationToken);

            stagedFiles.Add(new
            {
                AssetId = asset.Id,
                asset.OriginalFileName,
                LocalPath = targetFilePath,
                asset.FileUrl,
                asset.ContentType,
                asset.SizeBytes,
                asset.Extension
            });
        }

        return stagedFiles;
    }

    public static async Task WriteTaskManifestAsync(
        FileTask task,
        string taskDirectoryPath,
        IReadOnlyCollection<object> stagedInputs,
        IReadOnlyCollection<object> stagedTemplates)
    {
        var manifestPath = Path.Combine(taskDirectoryPath, "task-manifest.json");
        var manifest = new
        {
            FileTaskId = task.Id,
            task.TaskType,
            task.Status,
            task.Instructions,
            task.TaskDirectoryPath,
            Inputs = stagedInputs,
            Templates = stagedTemplates,
            task.CreatedAt,
            GeneratedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(manifestPath, json);

        if (!string.IsNullOrWhiteSpace(task.Instructions))
        {
            var instructionsPath = Path.Combine(taskDirectoryPath, "instructions.txt");
            await File.WriteAllTextAsync(instructionsPath, task.Instructions);
        }
    }

    private static string BuildUniqueTargetPath(string folderPath, string fileName)
    {
        var safeFileName = SanitizeFileName(fileName);
        var baseName = Path.GetFileNameWithoutExtension(safeFileName);
        var extension = Path.GetExtension(safeFileName);
        var candidatePath = Path.Combine(folderPath, safeFileName);
        var counter = 1;

        while (File.Exists(candidatePath))
        {
            candidatePath = Path.Combine(folderPath, $"{baseName}-{counter}{extension}");
            counter++;
        }

        return candidatePath;
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "unnamed-file" : sanitized;
    }
}