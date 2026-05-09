using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DevNexus.Domain.Abstractions;
using DevNexus.Shared.DTOs;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// 搜索供应商管理API
/// </summary>
[ApiController]
[Route("api/v1/providers/search")]
[Authorize(Roles = "Admin")]
public class SearchProviderController : ControllerBase
{
    private readonly ISearchProviderManagementService _service;
    private readonly ILogger<SearchProviderController> _logger;
    
    public SearchProviderController(
        ISearchProviderManagementService service,
        ILogger<SearchProviderController> logger)
    {
        _service = service;
        _logger = logger;
    }
    
    /// <summary>
    /// 获取所有搜索供应商
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SearchProviderResponse>), 200)]
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
    [ProducesResponseType(typeof(SearchProviderResponse), 200)]
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
    [ProducesResponseType(typeof(SearchProviderResponse), 200)]
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
    [ProducesResponseType(typeof(SearchProviderResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetDefaultProvider(
        CancellationToken cancellationToken = default)
    {
        var provider = await _service.GetDefaultProviderAsync(cancellationToken);
        return provider == null ? NotFound() : Ok(provider);
    }
    
    /// <summary>
    /// 创建新供应商
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SearchProviderResponse), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateProvider(
        [FromBody] CreateSearchProviderRequest request,
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
    [ProducesResponseType(typeof(SearchProviderResponse), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UpdateProvider(
        Guid id,
        [FromBody] UpdateSearchProviderRequest request,
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
    [ProducesResponseType(typeof(SearchProviderResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SetDefaultProvider(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var provider = await _service.SetDefaultProviderAsync(id, cancellationToken);
            return Ok(provider);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
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
    /// 启用/禁用供应商
    /// </summary>
    [HttpPost("{id:guid}/toggle")]
    [ProducesResponseType(typeof(SearchProviderResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ToggleProvider(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _service.GetProviderByIdAsync(id, cancellationToken);
        if (provider == null)
        {
            return NotFound();
        }
        
        var updated = await _service.UpdateProviderAsync(
            id,
            new UpdateSearchProviderRequest { IsEnabled = !provider.IsEnabled },
            cancellationToken);
            
        return Ok(updated);
    }

    /// <summary>
    /// 测试供应商连接 (创建前测试)
    /// </summary>
    [HttpPost("test")]
    [ProducesResponseType(typeof(ValidateProviderResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> TestConnection(
        [FromBody] CreateSearchProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.TestProviderConnectionAsync(
            request,
            cancellationToken);
        return Ok(result);
    }
}
