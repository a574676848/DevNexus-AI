// using DevNexus.Domain.Abstractions via GlobalUsings
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using DevNexus.Core.Abstractions;

namespace DevNexus.Infrastructure.Services.LLM;

public partial class KernelService
{
    /// <summary>
    /// 获取或创建 Kernel 实例（使用默认提供商）
    /// </summary>
    public async Task<Kernel> GetKernelAsync(CancellationToken cancellationToken = default)
    {
        EnsureCacheValid();

        if (_defaultKernel == null)
        {
            _currentProvider = await _providerFactory.GetDefaultProviderAsync(cancellationToken);
            var chatCompletionService = _currentProvider.GetChatCompletionService();

            var builder = Kernel.CreateBuilder();
            builder.Services.AddSingleton(chatCompletionService);
            builder.Services.AddSingleton<IAutoFunctionInvocationFilter>(_tokenAuditFilter);
            builder.Services.AddSingleton<IAutoFunctionInvocationFilter>(_toolExecutionCollectorFilter);
            builder.Services.AddSingleton<IAutoFunctionInvocationFilter>(sp => 
                new GlobalHostServiceInterceptor(
                    _confirmationService,
                    _loggerFactory.CreateLogger<GlobalHostServiceInterceptor>()
                )
            );

            _defaultKernel = builder.Build();
            RegisterPlugins(_defaultKernel);
            
            _logger.LogDebug(
                "[AI.Kernel] Kernel initialized with default provider | Provider={Provider}",
                _currentProvider.ProviderName);
        }

        return _defaultKernel;
    }

    /// <summary>
    /// 根据用户选择的 Provider ID 获取 Kernel 实例（无会话关联）
    /// </summary>
    /// <param name="providerId">数据库中 LLMProvider 的主键 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Kernel 实例</returns>
    public async Task<Kernel> GetKernelAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        EnsureCacheValid();

        // Provider-level cache implementation removed to simplify and fix issues. 
        // Always creating fresh kernel or relying on session cache is safer for now.
        // If provider-level caching is needed, it should be reintroduced carefully.
        
        var (kernel, provider) = await CreateKernelForProviderAsync(providerId, cancellationToken);
        _currentProvider = provider;

        _logger.LogDebug(
            "[AI.Kernel] Kernel initialized for provider | ProviderId={ProviderId} Provider={Provider}",
            providerId,
            provider.ProviderName);

        return kernel;
    }

    /// <summary>
    /// 根据 Provider ID 和 Session ID 获取 Kernel 实例（会话隔离）
    /// 每个会话拥有独立的 Kernel，确保插件上下文不会混淆
    /// </summary>
    /// <param name="providerId">数据库中 LLMProvider 的主键 ID</param>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="userId">用户 ID（用于插件上下文）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Kernel 实例</returns>
    public async Task<Kernel> GetKernelForSessionAsync(Guid providerId, Guid sessionId, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        EnsureCacheValid();

        var cacheKey = (providerId, sessionId);

        if (_sessionKernelCache.TryGetValue(cacheKey, out var cached))
        {
            _currentProvider = cached.provider;

            return cached.kernel;
        }

        var (kernel, provider) = await CreateKernelForProviderAsync(providerId, cancellationToken, sessionId, userId);
        _sessionKernelCache[cacheKey] = (kernel, provider);
        _currentProvider = provider;

        _logger.LogDebug(
            "[AI.Kernel] Kernel initialized for session | ProviderId={ProviderId} SessionId={SessionId} Provider={Provider}",
            providerId,
            sessionId,
            provider.ProviderName);

        return kernel;
    }

    /// <summary>
    /// 创建 Provider 对应的 Kernel 实例（内部方法）
    /// </summary>
    private async Task<(Kernel kernel, ILLMProvider provider)> CreateKernelForProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken,
        Guid? sessionId = null, // Optional sessionId
        Guid? userId = null)    // Optional userId
    {
        ILLMProvider provider;
        try
        {
            if (providerId == Guid.Empty)
            {
                provider = await _providerFactory.GetDefaultProviderAsync(cancellationToken);
                _logger.LogDebug("[AI.Kernel] providerId 为空，已获取系统默认的 Provider");
            }
            else
            {
                provider = await _providerFactory.GetProviderByIdAsync(providerId, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AI.Kernel] 获取指定 Provider({ProviderId}) 失败，正在回退到系统默认 Provider。", providerId);
            provider = await _providerFactory.GetDefaultProviderAsync(cancellationToken);
        }

        var chatCompletionService = provider.GetChatCompletionService();

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chatCompletionService);
        // Inject TokenAuditFilter
        builder.Services.AddSingleton<IAutoFunctionInvocationFilter>(_tokenAuditFilter);
        builder.Services.AddSingleton<IAutoFunctionInvocationFilter>(_toolExecutionCollectorFilter);
        
        // Inject GlobalHostServiceInterceptor
        builder.Services.AddSingleton<IAutoFunctionInvocationFilter>(sp => 
            new GlobalHostServiceInterceptor(
                _confirmationService,
                _loggerFactory.CreateLogger<GlobalHostServiceInterceptor>(),
                sessionId.HasValue ? sessionId.Value.ToString() : ""
            )
        );

        var kernel = builder.Build();
        
        // Register plugins with session context if available
        if (sessionId.HasValue)
        {
             RegisterPlugins(kernel, sessionId.Value, userId);
        }
        else
        {
             RegisterPlugins(kernel, null, null);
        }

        return (kernel, provider);
    }

    /// <summary>
    /// 获取当前使用的提供商名称
    /// </summary>
    public string GetCurrentProviderName()
    {
        return _currentProvider?.ProviderName ?? "None";
    }

    /// <summary>
    /// 清除指定 Provider 的 Kernel 缓存
    /// </summary>
    /// <param name="providerId">Provider ID，为 null 时清除所有缓存</param>
    /// <param name="sessionId">会话 ID，为 null 时清除该 Provider 的所有会话缓存</param>
    public void InvalidateKernelCache(Guid? providerId = null, Guid? sessionId = null)
    {
        if (providerId.HasValue && sessionId.HasValue)
        {
            // 清除特定会话的缓存
            var cacheKey = (providerId.Value, sessionId.Value);
            _sessionKernelCache.Remove(cacheKey);
            _logger.LogDebug(
                "[AI.Kernel] Invalidated kernel cache for session | ProviderId={ProviderId} SessionId={SessionId}",
                providerId, sessionId);
        }
        else if (providerId.HasValue)
        {
            // 清除指定 Provider 的所有缓存
            // _providerKernelCache.Remove(providerId.Value); // Removed

            // 同时清除该 Provider 的所有会话缓存
            var keysToRemove = _sessionKernelCache.Keys
                .Where(k => k.providerId == providerId.Value)
                .ToList();
            foreach (var key in keysToRemove)
            {
                _sessionKernelCache.Remove(key);
            }

            _logger.LogDebug(
                "[AI.Kernel] Invalidated all kernel cache for provider | ProviderId={ProviderId} SessionCount={Count}",
                providerId, keysToRemove.Count);
        }
        else
        {
            // 清除所有缓存
            // _providerKernelCache.Clear(); // Removed
            _sessionKernelCache.Clear();
            _defaultKernel = null;
            _currentProvider = null;
            _logger.LogDebug("[AI.Kernel] Invalidated all kernel cache");
        }

        // 同时清除 Provider Factory 的缓存
        _providerFactory.InvalidateCache(null);
    }

    /// <summary>
    /// 清除指定会话的 Kernel 缓存（会话结束时调用）
    /// </summary>
    public void InvalidateSessionKernelCache(Guid sessionId)
    {
        var keysToRemove = _sessionKernelCache.Keys
            .Where(k => k.sessionId == sessionId)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _sessionKernelCache.Remove(key);
        }

        if (keysToRemove.Count > 0)
        {
            _logger.LogDebug(
                "[AI.Kernel] Invalidated session kernel cache | SessionId={SessionId} Count={Count}",
                sessionId, keysToRemove.Count);
        }
    }
}
