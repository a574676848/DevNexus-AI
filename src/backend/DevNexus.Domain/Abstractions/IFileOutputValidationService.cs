using DevNexus.Domain.Models;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 文件任务输出验证服务接口
/// </summary>
public interface IFileOutputValidationService
{
    /// <summary>
    /// 验证文件任务生成的输出文件
    /// </summary>
    Task<FileOutputValidationResult> ValidateAsync(
        IReadOnlyCollection<string> generatedFiles,
        CancellationToken cancellationToken = default);
}