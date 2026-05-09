using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DevNexus.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Memory;

/// <summary>
/// 会话临时记忆服务实现
/// 
/// 存储策略：文件系统（UserTempPath/sessions/{sessionId}/memories/）
///</summary>
public class SessionMemoryService : ISessionMemoryService
{
    private readonly IUserStoragePathService _storagePathService;
    private readonly ILogger<SessionMemoryService> _logger;
    
    /// <summary>
    /// _index.md 最大大小限制（防止撑爆 System Prompt）
    /// </summary>
    private const int MaxIndexSize = 2000;
    
    public SessionMemoryService(IUserStoragePathService storagePathService, ILogger<SessionMemoryService> logger)
    {
        _storagePathService = storagePathService;
        _logger = logger;
    }
    
    private string GetMemoryDir(string userId, string sessionId)
    {
        var parsedUserId = System.Guid.TryParse(userId, out var guid) ? guid : System.Guid.Empty;
        var tmpPath = _storagePathService.GetUserTempPath(parsedUserId);
        return Path.Combine(tmpPath, "sessions", sessionId, "memories");
    }
    
    private string SanitizeName(string name)
    {
        var invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        var regex = new Regex($"[{Regex.Escape(invalidChars)}]");
        return regex.Replace(name, "_").ToLowerInvariant();
    }
    
    public async Task SaveAsync(string userId, string sessionId, string name, string category,
        string summary, string content, CancellationToken ct = default)
    {
        var dir = GetMemoryDir(userId, sessionId);
        Directory.CreateDirectory(dir);
        
        var filePath = Path.Combine(dir, $"{SanitizeName(name)}.md");
        var fullContent = $"# {name}\n\n> 分类: {category}\n> 更新时间: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}\n\n{content}";
        await File.WriteAllTextAsync(filePath, fullContent, ct);
        
        await UpdateIndexAsync(dir, name, category, summary, ct);
        
        _logger.LogInformation(
            "[SessionMemory] 保存记忆 | User={UserId} Session={Session} Name={Name} Category={Category}",
            userId, sessionId, name, category);
    }
    
    public async Task<string?> ReadAsync(string userId, string sessionId, string name, CancellationToken ct = default)
    {
        var filePath = Path.Combine(GetMemoryDir(userId, sessionId), $"{SanitizeName(name)}.md");
        if (!File.Exists(filePath)) return null;
        
        File.SetLastAccessTimeUtc(filePath, DateTime.UtcNow);
        return await File.ReadAllTextAsync(filePath, ct);
    }
    
    public async Task<string> GetIndexAsync(string userId, string sessionId, CancellationToken ct = default)
    {
        var indexPath = Path.Combine(GetMemoryDir(userId, sessionId), "_index.md");
        if (!File.Exists(indexPath)) return "（暂无会话记忆）";
        return await File.ReadAllTextAsync(indexPath, ct);
    }
    
    public async Task DeleteAsync(string userId, string sessionId, string name, CancellationToken ct = default)
    {
        var dir = GetMemoryDir(userId, sessionId);
        var filePath = Path.Combine(dir, $"{SanitizeName(name)}.md");
        if (File.Exists(filePath)) File.Delete(filePath);
        
        await RebuildIndexAsync(dir, ct);
        
        _logger.LogInformation(
            "[SessionMemory] 删除记忆 | User={UserId} Session={Session} Name={Name}",
            userId, sessionId, name);
    }
    
    public Task DeleteAllAsync(string userId, string sessionId, CancellationToken ct = default)
    {
        var dir = GetMemoryDir(userId, sessionId);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
            _logger.LogInformation(
                "[SessionMemory] 删除会话所有记忆 | User={UserId} Session={Session}",
                userId, sessionId);
        }
        return Task.CompletedTask;
    }
    

    
    private async Task UpdateIndexAsync(string dir, string name, string category,
        string summary, CancellationToken ct)
    {
        var indexPath = Path.Combine(dir, "_index.md");
        var entries = new Dictionary<string, (string Category, string Summary)>();
        
        if (File.Exists(indexPath))
        {
            var lines = await File.ReadAllLinesAsync(indexPath, ct);
            foreach (var line in lines.Where(l => l.StartsWith("- ")))
            {
                var match = Regex.Match(line, @"^- \[(.+?)\] (.+?) — (.+)$");
                if (match.Success)
                {
                    entries[match.Groups[2].Value.Trim()] = (
                        match.Groups[1].Value.Trim(),
                        match.Groups[3].Value.Trim());
                }
            }
        }
        
        entries[name] = (category, summary.Length > 100 ? summary[..100] : summary);
        await WriteIndexToFileAsync(indexPath, entries, ct);
    }
    
    private async Task RebuildIndexAsync(string dir, CancellationToken ct)
    {
        var indexPath = Path.Combine(dir, "_index.md");
        var entries = new Dictionary<string, (string Category, string Summary)>();
        
        if (File.Exists(indexPath))
        {
            var lines = await File.ReadAllLinesAsync(indexPath, ct);
            foreach (var line in lines.Where(l => l.StartsWith("- ")))
            {
                var match = Regex.Match(line, @"^- \[(.+?)\] (.+?) — (.+)$");
                if (match.Success)
                {
                    var entryName = match.Groups[2].Value.Trim();
                    // 只保留仍然存在的文件对应的条目
                    var expectedFile = Path.Combine(dir, $"{SanitizeName(entryName)}.md");
                    if (File.Exists(expectedFile))
                    {
                        entries[entryName] = (
                            match.Groups[1].Value.Trim(),
                            match.Groups[3].Value.Trim());
                    }
                }
            }
            await WriteIndexToFileAsync(indexPath, entries, ct);
        }
    }
    
    private async Task WriteIndexToFileAsync(string indexPath, Dictionary<string, (string Category, string Summary)> entries, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 📋 会话记忆目录");
        sb.AppendLine($"> 共 {entries.Count} 条记忆 | 如需查看详情请调用 `read_memory`");
        sb.AppendLine();
        
        foreach (var kvp in entries)
        {
            sb.AppendLine($"- [{kvp.Value.Category}] {kvp.Key} — {kvp.Value.Summary}");
        }
        
        var content = sb.ToString();
        if (content.Length > MaxIndexSize)
        {
            _logger.LogWarning("[SessionMemory] 记忆目录超过 {Max} 字符限制，请注意截断风险", MaxIndexSize);
            // 这里可以实现更复杂的 LRU 淘汰逻辑，目前先输出警告
        }
        
        await File.WriteAllTextAsync(indexPath, content, ct);
    }
}
