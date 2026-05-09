using DevNexus.Core.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Skills;

/// <summary>
/// 文件系统监控后台服务 - 监听 Skill 目录变更并自动触发热重载
/// 采用防抖策略：文件变更后等待 2 秒无新事件再执行重载，避免批量操作（如 ZIP 解压）引起频繁刷新
/// </summary>
public class SkillFileWatcherService : BackgroundService
{
    private readonly ISkillRegistry _skillRegistry;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SkillFileWatcherService> _logger;

    private readonly List<FileSystemWatcher> _watchers = new();

    // 防抖控制：每次文件事件到来时重置此 CTS，仅当 2 秒内无新事件才真正触发重载
    private CancellationTokenSource? _debounceCts;
    private readonly object _debounceLock = new();

    private static readonly TimeSpan DebounceDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// 构造函数
    /// </summary>
    public SkillFileWatcherService(
        ISkillRegistry skillRegistry,
        IConfiguration configuration,
        ILogger<SkillFileWatcherService> logger)
    {
        _skillRegistry = skillRegistry ?? throw new ArgumentNullException(nameof(skillRegistry));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var builtInPath = GetAbsolutePath(_configuration["Skills:BuiltInPath"] ?? "wwwroot/skills/built-in");
        var sharedPath = GetAbsolutePath(_configuration["Skills:SharedPath"] ?? "wwwroot/skills/custom/shared");
        var userPath = GetAbsolutePath(_configuration["Skills:UserPath"] ?? "wwwroot/skills/custom/user");

        // 为每个目录创建 Watcher
        StartWatching(builtInPath, "BuiltIn", stoppingToken);
        StartWatching(sharedPath, "Shared", stoppingToken);
        StartWatching(userPath, "User", stoppingToken);

        _logger.LogInformation("[Skill.FSW] 文件监控已启动 | BuiltIn={BuiltIn} Shared={Shared} User={User}",
            builtInPath, sharedPath, userPath);

        // BackgroundService 不需要长期运行的循环，FSW 通过事件回调工作
        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止服务时释放所有 Watcher
    /// </summary>
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Skill.FSW] 文件监控正在停止...");

        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();

        lock (_debounceLock)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
        }

        return base.StopAsync(cancellationToken);
    }

    // ==================== 私有方法 ====================

    /// <summary>
    /// 为指定目录启动文件监控
    /// </summary>
    private void StartWatching(string path, string label, CancellationToken stoppingToken)
    {
        if (!Directory.Exists(path))
        {
            _logger.LogWarning("[Skill.FSW] 目录不存在，跳过监控 | Label={Label} Path={Path}", label, path);
            return;
        }

        var watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName
                         | NotifyFilters.DirectoryName
                         | NotifyFilters.LastWrite
                         | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        // 监听所有文件类型（SKILL.md、*.yaml、scripts 等都可能影响 Skill 行为）
        watcher.Created += (_, e) => OnFileChanged(e.FullPath, "Created", label, stoppingToken);
        watcher.Changed += (_, e) => OnFileChanged(e.FullPath, "Changed", label, stoppingToken);
        watcher.Deleted += (_, e) => OnFileChanged(e.FullPath, "Deleted", label, stoppingToken);
        watcher.Renamed += (_, e) => OnFileChanged(e.FullPath, "Renamed", label, stoppingToken);
        watcher.Error += (_, e) => _logger.LogError(e.GetException(), "[Skill.FSW] 监控异常 | Label={Label}", label);

        _watchers.Add(watcher);
        _logger.LogDebug("[Skill.FSW] 监控已启动 | Label={Label} Path={Path}", label, path);
    }

    /// <summary>
    /// 文件变更回调 - 带防抖逻辑
    /// </summary>
    private void OnFileChanged(string fullPath, string changeType, string label, CancellationToken stoppingToken)
    {
        _logger.LogDebug("[Skill.FSW] 检测到变更 | Label={Label} Type={Type} Path={Path}", label, changeType, fullPath);

        lock (_debounceLock)
        {
            // 取消上一次的延迟任务
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var cts = _debounceCts;

            // 启动新的延迟任务
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(DebounceDelay, cts.Token);

                    // 延迟结束且未被取消，执行重载
                    _logger.LogInformation("[Skill.FSW] 防抖结束，触发热重载 | Label={Label}", label);
                    await _skillRegistry.ReloadAsync(CancellationToken.None);
                    _logger.LogInformation("[Skill.FSW] 热重载完成");
                }
                catch (OperationCanceledException)
                {
                    // 被新的文件事件取消，忽略（防抖生效）
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Skill.FSW] 热重载失败");
                }
            }, stoppingToken);
        }
    }

    /// <summary>
    /// 获取绝对路径（相对路径基于 AppDomain.CurrentDomain.BaseDirectory）
    /// </summary>
    private static string GetAbsolutePath(string path)
    {
        if (Path.IsPathRooted(path)) return path;
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
    }
}
