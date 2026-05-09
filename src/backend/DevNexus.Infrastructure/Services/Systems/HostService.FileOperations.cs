using System.Text;
using System.Text.Json;
using DevNexus.Core.Models.Execution;

namespace DevNexus.Infrastructure.Services.Systems;

/// <summary>
/// 宿主服务文件操作能力。
/// </summary>
public partial class HostService
{
    /// <inheritdoc />
    public async Task<HostTextOperationResult> ReadFileTextResultAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!ValidatePathAccess(fullPath))
            {
                return new HostTextOperationResult
                {
                    Status = HostOperationStatus.Failure,
                    Message = TaggedExecutionOutput.Parse(GetPermissionDeniedMessage(fullPath)).Message
                };
            }

            return new HostTextOperationResult
            {
                Status = HostOperationStatus.Success,
                Text = await File.ReadAllTextAsync(fullPath, cancellationToken),
                Message = "文件读取成功。"
            };
        }
        catch (UnauthorizedAccessException)
        {
            return new HostTextOperationResult
            {
                Status = HostOperationStatus.Failure,
                Message = $"操作系统权限拒绝：无法读取文件 '{path}'。请检查服务运行账户的系统权限。"
            };
        }
        catch (Exception ex)
        {
            return new HostTextOperationResult
            {
                Status = HostOperationStatus.Exception,
                Message = $"读取文件失败：{ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<HostOperationResult> WriteFileTextResultAsync(
        string path,
        string content,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!ValidatePathAccess(fullPath))
            {
                return new HostOperationResult
                {
                    Status = HostOperationStatus.Failure,
                    Message = TaggedExecutionOutput.Parse(GetPermissionDeniedMessage(fullPath)).Message
                };
            }

            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllTextAsync(fullPath, content, cancellationToken);
            return new HostOperationResult
            {
                Status = HostOperationStatus.Success,
                Message = "文件已成功写入。"
            };
        }
        catch (UnauthorizedAccessException)
        {
            return new HostOperationResult
            {
                Status = HostOperationStatus.Failure,
                Message = $"操作系统权限拒绝：无法写入文件 '{path}'。"
            };
        }
        catch (Exception ex)
        {
            return new HostOperationResult
            {
                Status = HostOperationStatus.Exception,
                Message = $"写入文件失败：{ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<HostTextOperationResult> ListDirectoryResultAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!ValidatePathAccess(fullPath))
            {
                return new HostTextOperationResult
                {
                    Status = HostOperationStatus.Failure,
                    Message = TaggedExecutionOutput.Parse(GetPermissionDeniedMessage(fullPath)).Message
                };
            }

            if (!Directory.Exists(fullPath))
            {
                return new HostTextOperationResult
                {
                    Status = HostOperationStatus.Failure,
                    Message = $"目录不存在: {path}"
                };
            }

            var di = new DirectoryInfo(fullPath);
            var items = di.GetFileSystemInfos()
                .Select(i => new
                {
                    Name = i.Name,
                    Type = i is DirectoryInfo ? "Directory" : "File",
                    Size = (i as FileInfo)?.Length ?? 0,
                    LastModified = i.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                })
                .ToList();

            return new HostTextOperationResult
            {
                Status = HostOperationStatus.Success,
                Text = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }),
                Message = "目录列出成功。"
            };
        }
        catch (Exception ex)
        {
            return new HostTextOperationResult
            {
                Status = HostOperationStatus.Exception,
                Message = $"列出目录失败：{ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<HostTextOperationResult> SearchInFilesResultAsync(
        string directory,
        string query,
        string filePattern = "*",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = Path.GetFullPath(directory);
            if (!ValidatePathAccess(fullPath))
            {
                return new HostTextOperationResult
                {
                    Status = HostOperationStatus.Failure,
                    Message = TaggedExecutionOutput.Parse(GetPermissionDeniedMessage(fullPath)).Message
                };
            }

            if (!Directory.Exists(fullPath))
            {
                return new HostTextOperationResult
                {
                    Status = HostOperationStatus.Failure,
                    Message = $"目录不存在: {directory}"
                };
            }

            var final = await Task.Run(() =>
            {
                var results = new StringBuilder();
                var files = Directory.EnumerateFiles(fullPath, filePattern, SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    var lines = File.ReadLines(file);
                    int lineNum = 1;
                    foreach (var line in lines)
                    {
                        if (line.Contains(query, StringComparison.OrdinalIgnoreCase))
                        {
                            results.AppendLine($"{Path.GetRelativePath(fullPath, file)}:{lineNum}: {line.Trim()}");
                        }

                        lineNum++;
                    }
                }

                return results.ToString();
            }, cancellationToken);

            if (string.IsNullOrEmpty(final))
            {
                return new HostTextOperationResult
                {
                    Status = HostOperationStatus.Info,
                    Message = "未发现匹配内容。"
                };
            }

            return new HostTextOperationResult
            {
                Status = HostOperationStatus.Success,
                Text = final,
                Message = "搜索完成。"
            };
        }
        catch (Exception ex)
        {
            return new HostTextOperationResult
            {
                Status = HostOperationStatus.Exception,
                Message = $"搜索文件失败：{ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<HostFileListOperationResult> ListFilesRecursiveResultAsync(
        string path,
        string[] patterns,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!ValidatePathAccess(fullPath))
            {
                return new HostFileListOperationResult
                {
                    Status = HostOperationStatus.Failure,
                    Message = TaggedExecutionOutput.Parse(GetPermissionDeniedMessage(fullPath)).Message
                };
            }

            if (!Directory.Exists(fullPath))
            {
                return new HostFileListOperationResult
                {
                    Status = HostOperationStatus.Failure,
                    Message = $"目录不存在：{fullPath}"
                };
            }

            var files = await Task.Run(() =>
            {
                var matchedFiles = new List<string>();
                foreach (var pattern in patterns)
                {
                    matchedFiles.AddRange(Directory.GetFiles(fullPath, pattern, SearchOption.AllDirectories));
                }

                return (IReadOnlyList<string>)matchedFiles.Distinct().ToList();
            }, cancellationToken);

            return new HostFileListOperationResult
            {
                Status = HostOperationStatus.Success,
                Message = "文件列表获取成功。",
                Files = files
            };
        }
        catch (Exception ex)
        {
            return new HostFileListOperationResult
            {
                Status = HostOperationStatus.Exception,
                Message = $"列出文件失败：{ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<HostOperationResult> ApplyDiffResultAsync(
        string path,
        string originalContent,
        string newContent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!ValidatePathAccess(fullPath))
            {
                return new HostOperationResult
                {
                    Status = HostOperationStatus.Failure,
                    Message = TaggedExecutionOutput.Parse(GetPermissionDeniedMessage(fullPath)).Message
                };
            }

            if (!File.Exists(fullPath))
            {
                return new HostOperationResult
                {
                    Status = HostOperationStatus.Failure,
                    Message = $"文件不存在：{path}"
                };
            }

            var fileContent = await File.ReadAllTextAsync(fullPath, cancellationToken);
            if (fileContent.Contains(originalContent))
            {
                var replaced = fileContent.Replace(originalContent, newContent);
                await File.WriteAllTextAsync(fullPath, replaced, cancellationToken);
                return new HostOperationResult
                {
                    Status = HostOperationStatus.Success,
                    Message = "文件已成功通过精确匹配更新。"
                };
            }

            var (normalizedFile, fileMap) = GetNormalizedWithMap(fileContent);
            var (normalizedOriginal, _) = GetNormalizedWithMap(originalContent);
            var matchIndex = normalizedFile.IndexOf(normalizedOriginal, StringComparison.Ordinal);

            if (matchIndex < 0)
            {
                return new HostOperationResult
                {
                    Status = HostOperationStatus.Failure,
                    Message = "无法在文件中找到匹配的原始内容。请检查 originalContent 是否与文件当前状态一致。"
                };
            }

            if (normalizedFile.LastIndexOf(normalizedOriginal, StringComparison.Ordinal) != matchIndex)
            {
                return new HostOperationResult
                {
                    Status = HostOperationStatus.Failure,
                    Message = "找到归一化匹配但该片段在文件中不唯一。请提供包含更多上下文的 originalContent。"
                };
            }

            var startOriginal = fileMap[matchIndex];
            var endNormalized = matchIndex + normalizedOriginal.Length - 1;
            var endOriginal = fileMap[endNormalized];

            var sb = new StringBuilder();
            sb.Append(fileContent[..startOriginal]);
            sb.Append(newContent);
            sb.Append(fileContent[(endOriginal + 1)..]);

            await File.WriteAllTextAsync(fullPath, sb.ToString(), cancellationToken);
            return new HostOperationResult
            {
                Status = HostOperationStatus.Success,
                Message = "文件已通过空白容错坐标映射成功更新。"
            };
        }
        catch (Exception ex)
        {
            return new HostOperationResult
            {
                Status = HostOperationStatus.Exception,
                Message = $"应用差异失败：{ex.Message}"
            };
        }
    }
}
