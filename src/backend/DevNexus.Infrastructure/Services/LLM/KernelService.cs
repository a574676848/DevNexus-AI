using DevNexus.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace DevNexus.Infrastructure.Services.LLM;

/// <summary>
/// Semantic Kernel 服务封装
/// 提供统一的 AI 聊天完成接口
/// </summary>
public partial class KernelService : IKernelService
{
    private readonly ILLMProviderFactory _providerFactory;
    private readonly ITokenAuditService _tokenAuditService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<KernelService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TokenAuditFilter _tokenAuditFilter;
    private readonly ToolExecutionCollectorFilter _toolExecutionCollectorFilter;
    private readonly IConfirmationService _confirmationService;

    // 基于 (ProviderId, SessionId) 复合键缓存 Kernel 实例
    // 每个会话拥有独立的 Kernel，确保插件上下文不会混淆
    private readonly Dictionary<(Guid providerId, Guid sessionId), (Kernel kernel, ILLMProvider provider)> _sessionKernelCache = new();

    // 仅用于非会话场景的 Provider 级别缓存
    private readonly Dictionary<Guid, (Kernel kernel, ILLMProvider provider)> _providerKernelCache = new();
    private Kernel? _defaultKernel;
    private ILLMProvider? _currentProvider;

    private readonly ISkillRegistry _skillRegistry;
    private readonly LLMProviderCacheState _providerCacheState;

    /// <summary>
    /// 构造函数
    /// </summary>
    public KernelService(
        ILLMProviderFactory providerFactory,
        ITokenAuditService tokenAuditService,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        TokenAuditFilter tokenAuditFilter,
        Core.Abstractions.ISkillRegistry skillRegistry,
        LLMProviderCacheState providerCacheState)
    {
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _tokenAuditService = tokenAuditService ?? throw new ArgumentNullException(nameof(tokenAuditService));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<KernelService>();
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _tokenAuditFilter = tokenAuditFilter ?? throw new ArgumentNullException(nameof(tokenAuditFilter));
        _toolExecutionCollectorFilter = serviceProvider.GetRequiredService<ToolExecutionCollectorFilter>();
        _skillRegistry = skillRegistry ?? throw new ArgumentNullException(nameof(skillRegistry));
        _providerCacheState = providerCacheState ?? throw new ArgumentNullException(nameof(providerCacheState));
        _confirmationService = serviceProvider.GetRequiredService<IConfirmationService>();
    }

    private long _currentCacheVersion = -1;
    private long _currentProviderCacheVersion = -1;

    private void EnsureCacheValid()
    {
        var skillVersion = _skillRegistry.StateVersion;
        var providerVersion = _providerCacheState.Version;
        var shouldInvalidate = false;

        if (_currentCacheVersion != skillVersion)
        {
            if (_currentCacheVersion != -1) // 忽略初次初始化
            {
                _logger.LogInformation("[KernelService] 检测到 Skill 状态版本变化 ({Old} -> {New}), 正在失效 Kernel 缓存", _currentCacheVersion, skillVersion);
            }

            _currentCacheVersion = skillVersion;
            shouldInvalidate = true;
        }

        if (_currentProviderCacheVersion != providerVersion)
        {
            if (_currentProviderCacheVersion != -1)
            {
                _logger.LogInformation("[KernelService] 检测到 Provider 配置版本变化 ({Old} -> {New}), 正在失效 Kernel 缓存", _currentProviderCacheVersion, providerVersion);
            }

            _currentProviderCacheVersion = providerVersion;
            shouldInvalidate = true;
        }

        if (shouldInvalidate)
        {
            _sessionKernelCache.Clear();
            _providerKernelCache.Clear();
            _defaultKernel = null;
        }
    }
}
