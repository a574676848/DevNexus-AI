using DevNexus.Domain.Abstractions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// Embedding供应商管理API
/// </summary>
[ApiController]
[Route("api/v1/providers/embedding")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class EmbeddingProviderController : ControllerBase
{
    private readonly IEmbeddingProviderManagementService _service;
    private readonly ILogger<EmbeddingProviderController> _logger;

    public EmbeddingProviderController(
        IEmbeddingProviderManagementService service,
        ILogger<EmbeddingProviderController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有Embedding供应商
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EmbeddingProviderResponse>), 200)]
    public async Task<IActionResult> GetAllProviders(
        [FromQuery] bool includeDisabled = false,
        CancellationToken cancellationToken = default)
    {
        var providers = await _service.GetAllProvidersAsync(
            includeDisabled,
            cancellationToken);
        return Ok(providers);
    }

    /// <summary>
    /// 根据ID获取供应商
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmbeddingProviderResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetProviderById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _service.GetProviderByIdAsync(id, cancellationToken);
        return provider == null ? NotFound() : Ok(provider);
    }

    /// <summary>
    /// 根据ProviderId获取供应商
    /// </summary>
    [HttpGet("by-provider-id/{providerId}")]
    [ProducesResponseType(typeof(EmbeddingProviderResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetProviderByProviderId(
        string providerId,
        CancellationToken cancellationToken = default)
    {
        var provider = await _service.GetProviderByProviderIdAsync(providerId, cancellationToken);
        return provider == null ? NotFound() : Ok(provider);
    }

    /// <summary>
    /// 获取默认供应商
    /// </summary>
    [HttpGet("default")]
    [ProducesResponseType(typeof(EmbeddingProviderResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetDefaultProvider(
        CancellationToken cancellationToken = default)
    {
        var provider = await _service.GetDefaultProviderAsync(cancellationToken);
        return provider == null ? NotFound() : Ok(provider);
    }

    /// <summary>
    /// 获取全局向量维度配置
    /// </summary>
    [HttpGet("global-vector-size")]
    [ProducesResponseType(typeof(object), 200)]
    public IActionResult GetGlobalVectorSize()
    {
        return Ok(new { vectorSize = _service.GetGlobalVectorSize() });
    }

    /// <summary>
    /// 创建新供应商
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmbeddingProviderResponse), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateProvider(
        [FromBody] CreateEmbeddingProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var provider = await _service.CreateProviderAsync(request, cancellationToken);
            return CreatedAtAction(
                nameof(GetProviderById),
                new { id = provider.Id },
                provider);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// 更新供应商
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmbeddingProviderResponse), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UpdateProvider(
        Guid id,
        [FromBody] UpdateEmbeddingProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var provider = await _service.UpdateProviderAsync(
                id,
                request,
                cancellationToken);
            return Ok(provider);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// 删除供应商
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteProvider(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.DeleteProviderAsync(id, cancellationToken);
        return result ? NoContent() : NotFound();
    }

    /// <summary>
    /// 设置默认供应商
    /// </summary>
    [HttpPost("{id:guid}/set-default")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SetDefaultProvider(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.SetDefaultProviderAsync(id, cancellationToken);
        return result ? NoContent() : NotFound();
    }

    /// <summary>
    /// 验证供应商配置
    /// </summary>
    [HttpPost("{id:guid}/validate")]
    [ProducesResponseType(typeof(ValidateProviderResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ValidateProvider(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _service.ValidateProviderAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// 测试供应商连接 (创建前测试)
    /// </summary>
    [HttpPost("test")]
    [ProducesResponseType(typeof(ValidateProviderResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> TestConnection(
        [FromBody] CreateEmbeddingProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.TestProviderConnectionAsync(
            request,
            cancellationToken);
        return Ok(result);
    }
}
