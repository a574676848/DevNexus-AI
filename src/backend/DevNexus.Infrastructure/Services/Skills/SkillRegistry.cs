using System.Collections.Concurrent;
using System.IO.Compression;
using Microsoft.AspNetCore.Hosting;
using DevNexus.Core.Abstractions;
using DevNexus.Domain.Models;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DevNexus.Infrastructure.Services.Skills;

/// <summary>
/// Skill 注册中心实现 - 文件系统扫描 + YAML 解析 + 内存缓存
/// </summary>
public class SkillRegistry : ISkillRegistry
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SkillRegistry> _logger;
    private readonly ConcurrentDictionary<string, SkillMetadata> _skillCache = new();
    private readonly IDeserializer _yamlDeserializer;
    private volatile bool _initialized;

    private readonly IWebHostEnvironment _env;
    private long _stateVersion = 0;

    /// <summary>
    /// 全局状态版本戳，发生任何加载/删除/重载时递增
    /// 用于下游（如 KernelService）惰性失效缓存
    /// </summary>
    public long StateVersion => Interlocked.Read(ref _stateVersion);

    /// <summary>
    /// 初始化 Skill 注册中心
    /// </summary>
    public SkillRegistry(IConfiguration configuration, ILogger<SkillRegistry> logger, IWebHostEnvironment env)
    {
        _configuration = configuration;
        _logger = logger;
        _env = env;

        // 使用下划线分隔命名约定，兼容 YAML frontmatter 中的 kebab-case
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        var builtInPath = GetAbsolutePath(_configuration["Skills:BuiltInPath"] ?? "wwwroot/skills/built-in");
        var sharedPath = GetAbsolutePath(_configuration["Skills:SharedPath"] ?? "wwwroot/skills/custom/shared");
        var userPath = GetAbsolutePath(_configuration["Skills:UserPath"] ?? "wwwroot/skills/custom/user");

        _logger.LogInformation("[Skill.Registry] 初始化开始 | BuiltInPath={BuiltIn} SharedPath={Shared} UserPath={User}",
            builtInPath, sharedPath, userPath);

        // 扫描内置 Skill
        await ScanDirectoryAsync(builtInPath, SkillScope.BuiltIn, cancellationToken);

        // 扫描共享 Skill
        await ScanDirectoryAsync(sharedPath, SkillScope.Shared, cancellationToken);

        // 扫描用户私有 Skill (User 目录下按 UserId 子目录存放)
        await ScanUserDirectoriesAsync(userPath, cancellationToken);

        _initialized = true;
        _logger.LogInformation("[Skill.Registry] 初始化完成 | 已加载 {Count} 个 Skill", _skillCache.Count);
    }

    /// <inheritdoc />
    public IReadOnlyList<SkillMetadata> GetAllEnabled()
    {
        return _skillCache.Values
            .Where(s => s.Enabled)
            .OrderByDescending(s => s.Priority)
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<SkillMetadata> GetAvailableSkills(Guid userId)
    {
        return _skillCache.Values
            .Where(s => s.Enabled &&
                (s.Scope == SkillScope.BuiltIn ||
                 s.Scope == SkillScope.Shared ||
                 (s.Scope == SkillScope.User && s.OwnerUserId == userId)))
            .OrderByDescending(s => s.Priority)
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public SkillMetadata? GetByName(string name)
    {
        _skillCache.TryGetValue(name, out var skill);
        return skill;
    }

    /// <inheritdoc />
    public async Task<string> LoadInstructionAsync(string skillName, CancellationToken ct = default)
    {
        var skill = GetByName(skillName);
        if (skill == null)
        {
            _logger.LogWarning("[Skill.Registry] 加载指令失败：Skill 不存在 | Name={Name}", skillName);
            return string.Empty;
        }

        // L2 惰性加载：首次读取后缓存
        if (skill.InstructionContent != null)
        {
            return skill.InstructionContent;
        }

        var skillMdPath = Path.Combine(skill.DirectoryPath, "SKILL.md");
        if (!File.Exists(skillMdPath))
        {
            _logger.LogWarning("[Skill.Registry] SKILL.md 文件不存在 | Path={Path}", skillMdPath);
            return string.Empty;
        }

        var fullContent = await File.ReadAllTextAsync(skillMdPath, ct);
        var instruction = ExtractBodyContent(fullContent);

        skill.InstructionContent = instruction;
        _logger.LogDebug("[Skill.Registry] 已加载 L2 指令 | Skill={Name} Length={Length}",
            skillName, instruction.Length);

        return instruction;
    }

    /// <inheritdoc />
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Skill.Registry] 热重载开始");
        _skillCache.Clear();
        _initialized = false;
        await InitializeAsync(cancellationToken);

        // 通知 Kernel 缓存失效 (版本自增)
        Interlocked.Increment(ref _stateVersion);
        _logger.LogInformation("[Skill.Registry] 热重载完成，已通知缓存失效");
    }

    /// <inheritdoc />
    public async Task<SkillMetadata> RegisterAsync(
        string skillDirectory, SkillScope scope,
        Guid? userId = null, CancellationToken ct = default)
    {
        var skill = await ParseSkillDirectoryAsync(skillDirectory, scope, ct);
        if (skill == null)
        {
            throw new InvalidOperationException($"无法解析 Skill 目录: {skillDirectory}");
        }

        skill.OwnerUserId = userId;
        _skillCache[skill.Name] = skill;
        _logger.LogInformation("[Skill.Registry] 注册 Skill | Name={Name} Scope={Scope}", skill.Name, scope);

        Interlocked.Increment(ref _stateVersion);
        return skill;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string skillName, CancellationToken ct = default)
    {
        if (_skillCache.TryRemove(skillName, out _))
        {
            _logger.LogInformation("[Skill.Registry] 移除 Skill | Name={Name}", skillName);
            Interlocked.Increment(ref _stateVersion);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 解压并导入 Skill 压缩包（支持单个或多个 Skill）
    /// </summary>
    public async Task<List<SkillMetadata>> ImportSkillArchiveAsync(
        Stream archiveStream, 
        SkillScope scope, 
        Guid? userId = null, 
        CancellationToken ct = default)
    {
        var targetBaseDir = scope switch
        {
            SkillScope.Shared => GetAbsolutePath(_configuration["Skills:SharedPath"] ?? "wwwroot/skills/custom/shared"),
            SkillScope.User => Path.Combine(GetAbsolutePath(_configuration["Skills:UserPath"] ?? "wwwroot/skills/custom/user"), userId?.ToString() ?? "unknown"),
            _ => throw new ArgumentException($"不支持导入到 {scope} 作用域，请选择全局或用户作用域", nameof(scope))
        };

        if (!Directory.Exists(targetBaseDir))
        {
            Directory.CreateDirectory(targetBaseDir);
        }

        // 创建临时目录解压 (创建在 targetBaseDir 下以确保在同一磁盘分区，避免 Directory.Move 跨卷错误)
        var tempDir = Path.Combine(targetBaseDir, ".import_tmp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var importedSkills = new List<SkillMetadata>();

        try
        {
            // 解压 ZIP 文件
            using (var archive = new System.IO.Compression.ZipArchive(archiveStream, System.IO.Compression.ZipArchiveMode.Read))
            {
                archive.ExtractToDirectory(tempDir, overwriteFiles: true);
            }

            // 寻找所有包含 SKILL.md 的目录
            var skillDirs = Directory.GetFiles(tempDir, "SKILL.md", SearchOption.AllDirectories)
                                     .Select(Path.GetDirectoryName)
                                     .Where(d => !string.IsNullOrEmpty(d))
                                     .ToList();

            if (skillDirs.Count == 0)
            {
                throw new InvalidOperationException("压缩包中找不到任何 SKILL.md 文件");
            }

            foreach (var actualSkillDir in skillDirs)
            {
                // 先解析一遍，获取 Name 用于确定最终目录名
                var testParse = await ParseSkillDirectoryAsync(actualSkillDir!, scope, ct);
                if (testParse == null || string.IsNullOrWhiteSpace(testParse.Name))
                {
                    _logger.LogWarning("[Skill.Registry] 跳过无效 Skill 目录（缺失或不合法的 name）| Path={Path}", actualSkillDir);
                    continue;
                }

                // 目标目录名必须是 skill 的 Name
                var finalTargetDir = Path.Combine(targetBaseDir, testParse.Name);

                // 如果存在，先删除旧目录
                if (Directory.Exists(finalTargetDir))
                {
                    Directory.Delete(finalTargetDir, recursive: true);
                }

                // 移动解压出来的目录到最终目标地
                Directory.Move(actualSkillDir!, finalTargetDir);

                // 正式注册进缓存
                var skill = await RegisterAsync(finalTargetDir, scope, userId, ct);
                importedSkills.Add(skill);
            }

            return importedSkills;
        }
        finally
        {
            // 清理临时目录
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { /* 忽略清理错误 */ }
            }
        }
    }

    // ==================== 私有方法 ====================

    /// <summary>
    /// 扫描指定目录下的所有 Skill 子目录
    /// </summary>
    private async Task ScanDirectoryAsync(
        string rootPath, SkillScope scope, CancellationToken ct)
    {
        if (!Directory.Exists(rootPath))
        {
            _logger.LogDebug("[Skill.Registry] 目录不存在，跳过扫描 | Path={Path}", rootPath);
            return;
        }

        foreach (var skillDir in Directory.GetDirectories(rootPath))
        {
            var dirName = Path.GetFileName(skillDir);
            if (dirName.StartsWith(".")) continue;

            try
            {
                var skill = await ParseSkillDirectoryAsync(skillDir, scope, ct);
                if (skill != null)
                {
                    _skillCache[skill.Name] = skill;
                    _logger.LogDebug("[Skill.Registry] 已加载 Skill | Name={Name} Type={Type} Scope={Scope}",
                        skill.Name, skill.Type, scope);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Skill.Registry] 解析 Skill 目录失败 | Path={Path}", skillDir);
            }
        }
    }

    /// <summary>
    /// 扫描 User 级私有 Skill 目录
    /// 结构：user/{userId}/{skill-name}/SKILL.md
    /// </summary>
    private async Task ScanUserDirectoriesAsync(string userBasePath, CancellationToken ct)
    {
        if (!Directory.Exists(userBasePath)) return;

        foreach (var userDir in Directory.GetDirectories(userBasePath))
        {
            var userIdStr = Path.GetFileName(userDir);
            if (userIdStr.StartsWith(".")) continue;

            if (Guid.TryParse(userIdStr, out var userId))
            {
                // 用 ScanDirectoryAsync 解析该用户的子目录，但需要修改 ParseSkillDirectoryAsync 来接受 OwnerUserId，
                // 由于目前 ParseSkillDirectoryAsync 不传 OwnerUserId，我们手动遍历：
                foreach (var skillDir in Directory.GetDirectories(userDir))
                {
                    var skillNameDir = Path.GetFileName(skillDir);
                    if (skillNameDir.StartsWith(".")) continue;

                    try
                    {
                        var skill = await ParseSkillDirectoryAsync(skillDir, SkillScope.User, ct);
                        if (skill != null)
                        {
                            skill.OwnerUserId = userId; // 标记所有者
                            _skillCache[skill.Name] = skill;
                            _logger.LogDebug("[Skill.Registry] 已加载用户私有 Skill | User={UserId} Name={Name}",
                                userId, skill.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[Skill.Registry] 解析 User Skill 失败 | Path={Path}", skillDir);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 解析单个 Skill 目录，读取 SKILL.md 的 YAML frontmatter
    /// </summary>
    private async Task<SkillMetadata?> ParseSkillDirectoryAsync(
        string skillDir, SkillScope scope, CancellationToken ct)
    {
        var skillMdPath = Path.Combine(skillDir, "SKILL.md");
        if (!File.Exists(skillMdPath))
        {
            _logger.LogDebug("[Skill.Registry] SKILL.md 不存在，跳过 | Dir={Dir}", skillDir);
            return null;
        }

        var content = await File.ReadAllTextAsync(skillMdPath, ct);
        var frontmatter = ExtractFrontmatter(content);

        if (string.IsNullOrWhiteSpace(frontmatter))
        {
            _logger.LogWarning("[Skill.Registry] SKILL.md 缺少 YAML frontmatter，跳过 | Path={Path}", skillMdPath);
            return null;
        }

        // 解析 YAML frontmatter
        var yamlData = _yamlDeserializer.Deserialize<Dictionary<string, object>>(frontmatter);
        if (yamlData == null)
        {
            _logger.LogWarning("[Skill.Registry] YAML frontmatter 解析为空 | Path={Path}", skillMdPath);
            return null;
        }

        // 校验必填字段
        if (!yamlData.TryGetValue("name", out var nameObj) || string.IsNullOrWhiteSpace(nameObj?.ToString()))
        {
            _logger.LogWarning("[Skill.Registry] YAML frontmatter 缺少必填字段 'name' | Path={Path}", skillMdPath);
            return null;
        }

        if (!yamlData.TryGetValue("description", out var descObj) || string.IsNullOrWhiteSpace(descObj?.ToString()))
        {
            _logger.LogWarning("[Skill.Registry] YAML frontmatter 缺少必填字段 'description' | Path={Path}", skillMdPath);
            return null;
        }

        var name = nameObj.ToString()!.Trim();
        var description = descObj.ToString()!.Trim();

        // 构建 SkillMetadata
        var skill = new SkillMetadata
        {
            Name = name,
            Description = description,
            DirectoryPath = Path.GetFullPath(skillDir),
            Scope = scope,
            // 官方规范可选字段
            License = GetStringValue(yamlData, "license"),
            Compatibility = GetStringValue(yamlData, "compatibility"),
            AllowedTools = GetStringValue(yamlData, "allowed-tools"),
            UpdatedAt = File.GetLastWriteTime(skillMdPath)
        };

        // DevNexus 扩展字段（从 metadata 或直接字段读取）
        ParseExtendedFields(yamlData, skill);

        // 扫描物理文件 (Scripts & References)
        ScanPhysicalFiles(skill);

        // 根据内容推断类型（如果未显式指定）
        if (skill.Type == SkillType.PromptOnly)
        {
            if (skill.Plugins.Count > 0 && skill.Scripts.Count > 0)
                skill.Type = SkillType.Hybrid;
            else if (skill.Plugins.Count > 0)
                skill.Type = SkillType.PluginBound;
            else if (skill.Scripts.Count > 0 || Directory.Exists(Path.Combine(skillDir, "scripts")))
                skill.Type = SkillType.Script;
        }

        // 如果 SKILL.md 中有正文内容，立即标记，以确保前端 HasInstruction 标志正确
        var bodyContent = ExtractBodyContent(content);
        if (!string.IsNullOrWhiteSpace(bodyContent))
        {
            skill.InstructionContent = bodyContent;
        }

        return skill;
    }

    /// <summary>
    /// 提取 YAML frontmatter（--- 包裹的部分）
    /// </summary>
    private static string? ExtractFrontmatter(string content)
    {
        if (!content.TrimStart().StartsWith("---"))
            return null;

        var firstDelim = content.IndexOf("---", StringComparison.Ordinal);
        var secondDelim = content.IndexOf("---", firstDelim + 3, StringComparison.Ordinal);

        if (secondDelim < 0) return null;

        return content.Substring(firstDelim + 3, secondDelim - firstDelim - 3).Trim();
    }

    /// <summary>
    /// 提取 SKILL.md 正文内容（frontmatter 之后的部分）
    /// </summary>
    private static string ExtractBodyContent(string content)
    {
        if (!content.TrimStart().StartsWith("---"))
            return content;

        var firstDelim = content.IndexOf("---", StringComparison.Ordinal);
        var secondDelim = content.IndexOf("---", firstDelim + 3, StringComparison.Ordinal);

        if (secondDelim < 0)
            return content;

        return content.Substring(secondDelim + 3).TrimStart('\r', '\n');
    }

    /// <summary>
    /// 解析 DevNexus 扩展字段
    /// </summary>
    private static void ParseExtendedFields(Dictionary<string, object> yamlData, SkillMetadata skill)
    {
        // 类型
        if (yamlData.TryGetValue("type", out var typeObj))
        {
            var typeStr = typeObj.ToString()?.ToLowerInvariant();
            skill.Type = typeStr switch
            {
                "prompt-only" => SkillType.PromptOnly,
                "script" => SkillType.Script,
                "plugin-bound" => SkillType.PluginBound,
                "hybrid" => SkillType.Hybrid,
                _ => SkillType.PromptOnly
            };
        }

        // 插件列表
        if (yamlData.TryGetValue("plugins", out var pluginsObj) && pluginsObj is List<object> pluginsList)
        {
            skill.Plugins = pluginsList.Select(p => p.ToString()!).ToList();
        }

        // 标签
        if (yamlData.TryGetValue("tags", out var tagsObj) && tagsObj is List<object> tagsList)
        {
            skill.Tags = tagsList.Select(t => t.ToString()!).ToList();
        }

        // 触发模式
        if (yamlData.TryGetValue("trigger-patterns", out var patternsObj) && patternsObj is List<object> patternsList)
        {
            skill.TriggerPatterns = patternsList.Select(p => p.ToString()!).ToList();
        }
        // 兼容下划线命名
        else if (yamlData.TryGetValue("trigger_patterns", out patternsObj) && patternsObj is List<object> patternsList2)
        {
            skill.TriggerPatterns = patternsList2.Select(p => p.ToString()!).ToList();
        }

        // 自动触发
        if (yamlData.TryGetValue("auto-trigger", out var autoObj))
        {
            skill.AutoTrigger = autoObj is bool b ? b : bool.TryParse(autoObj.ToString(), out var parsed) && parsed;
        }

        // 优先级
        if (yamlData.TryGetValue("priority", out var priObj) && int.TryParse(priObj.ToString(), out var pri))
        {
            skill.Priority = pri;
        }

        // 启用状态
        if (yamlData.TryGetValue("enabled", out var enabledObj))
        {
            skill.Enabled = enabledObj is bool eb ? eb : !bool.TryParse(enabledObj.ToString(), out var ep) || ep;
        }

        // 版本和作者
        skill.Version = GetStringValue(yamlData, "version") ?? "1.0.0";
        skill.Author = GetStringValue(yamlData, "author") ?? string.Empty;

        // metadata 键值对
        if (yamlData.TryGetValue("metadata", out var metaObj) && metaObj is Dictionary<object, object> metaDict)
        {
            skill.Metadata = metaDict.ToDictionary(
                kv => kv.Key.ToString()!,
                kv => kv.Value?.ToString() ?? string.Empty);
        }

        // 脚本列表
        if (yamlData.TryGetValue("scripts", out var scriptsObj) && scriptsObj is List<object> scriptsList)
        {
            foreach (var scriptItem in scriptsList)
            {
                if (scriptItem is Dictionary<object, object> scriptDict)
                {
                    skill.Scripts.Add(new SkillScript
                    {
                        Path = scriptDict.TryGetValue("path", out var p) ? p?.ToString() ?? "" : "",
                        Runtime = scriptDict.TryGetValue("runtime", out var r) ? r?.ToString() ?? "pwsh" : "pwsh",
                        Description = scriptDict.TryGetValue("description", out var d) ? d?.ToString() ?? "" : "",
                        Timeout = scriptDict.TryGetValue("timeout", out var t) && int.TryParse(t?.ToString(), out var tv) ? tv : 30000
                    });
                }
            }
        }

        // 参考资料
        if (yamlData.TryGetValue("references", out var refsObj) && refsObj is List<object> refsList)
        {
            skill.References = refsList.Select(r => r.ToString()!).ToList();
        }
    }

    /// <summary>
    /// 从 YAML 字典安全获取字符串值
    /// </summary>
    private static string? GetStringValue(Dictionary<string, object> data, string key)
    {
        return data.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    /// <summary>
    /// 扫描物理文件以自动补充 Scripts 和 References (避免覆盖 YAML 中已有的配置)
    /// </summary>
    private static void ScanPhysicalFiles(SkillMetadata skill)
    {
        // 1. 扫描 scripts 目录
        var scriptsDir = Path.Combine(skill.DirectoryPath, "scripts");
        if (Directory.Exists(scriptsDir))
        {
            var scriptFiles = Directory.GetFiles(scriptsDir, "*.*", SearchOption.AllDirectories);
            foreach (var file in scriptFiles)
            {
                var relativePath = Path.GetRelativePath(skill.DirectoryPath, file).Replace("\\", "/");
                
                // 跳过隐藏文件或特定文件
                var fileName = Path.GetFileName(file).ToLowerInvariant();
                if (fileName.StartsWith(".")) continue;
                
                // 如果 YAML 尚未显式定义此脚本，将其自动包含
                if (!skill.Scripts.Any(s => string.Equals(s.Path.Replace("\\", "/"), relativePath, StringComparison.OrdinalIgnoreCase)))
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    // 这里推断如果是在 scripts/ 下的 md 文件，不作为 reference 而是作为文档（如果是脚本内的配置），但前面需求说 pdf 把 forms.md 放在 references/ 之外，其实 pdf 例子是 forms.md 在 scripts 下吗？
                    // "如果单个reference 可能跟skill同目录，也可能在references 目录下"
                    // 所以脚本目录里我们主要提取脚本后缀（或所有文件除了 md/txt？）
                    // 保守起见，将所有文件都添加，但仅对常见脚本赋予运行时
                    var runtime = ext switch
                    {
                        ".py" => "python",
                        ".ps1" => "pwsh",
                        ".sh" => "bash",
                        ".js" => "node",
                        _ => "unknown"
                    };
                    
                    if (runtime != "unknown")
                    {
                        skill.Scripts.Add(new SkillScript
                        {
                            Path = relativePath,
                            Runtime = runtime,
                            Description = ""
                        });
                    }
                }
            }
        }

        // 2. 扫描 references 目录
        var refsDir = Path.Combine(skill.DirectoryPath, "references");
        if (Directory.Exists(refsDir))
        {
            var refFiles = Directory.GetFiles(refsDir, "*.*", SearchOption.AllDirectories);
            foreach (var file in refFiles)
            {
                var relativePath = Path.GetRelativePath(skill.DirectoryPath, file).Replace("\\", "/");
                if (!skill.References.Any(r => string.Equals(r.Replace("\\", "/"), relativePath, StringComparison.OrdinalIgnoreCase)))
                {
                    skill.References.Add(relativePath);
                }
            }
        }

        // 3. 扫描根目录的 reference 相关文件 (.md, .txt)
        var rootFiles = Directory.GetFiles(skill.DirectoryPath, "*.*", SearchOption.TopDirectoryOnly);
        foreach (var file in rootFiles)
        {
            var name = Path.GetFileName(file).ToLowerInvariant();
            
            // 排除系统文件和核心文件
            if (name == "skill.md" || name == "license.txt" || name.StartsWith("."))
                continue;
                
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".md" || ext == ".txt" || name.Contains("reference"))
            {
                var relativePath = Path.GetRelativePath(skill.DirectoryPath, file).Replace("\\", "/");
                if (!skill.References.Any(r => string.Equals(r.Replace("\\", "/"), relativePath, StringComparison.OrdinalIgnoreCase)))
                {
                    skill.References.Add(relativePath);
                }
            }
        }
    }

    /// <summary>
    /// 获取绝对路径（兼容调试环境和发布环境）
    /// </summary>
    private string GetAbsolutePath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
            return relativePath;

        // 1. 优先尝试相对于 ContentRootPath (项目源码根目录或发布后的根目录)
        var path = Path.Combine(_env.ContentRootPath, relativePath);
        if (Directory.Exists(path) || File.Exists(path))
            return path;

        // 2. 如果不存在，尝试相对于执行目录 (bin/Debug/...)
        path = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (Directory.Exists(path) || File.Exists(path))
            return path;

        // 3. 最后回退到原始组合，由后续逻辑处理不存在的情况
        return Path.GetFullPath(Path.Combine(_env.ContentRootPath, relativePath));
    }
}
