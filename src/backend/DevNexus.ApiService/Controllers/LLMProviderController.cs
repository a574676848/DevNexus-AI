using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DevNexus.Domain.Abstractions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// LLM供应商管理API
/// </summary>
[ApiController]
[Route("api/v1/providers/llm")]
[Authorize]
public class LLMProviderController : ControllerBase
{
    private readonly ILLMProviderManagementService _service;
    private readonly ILogger<LLMProviderController> _logger;
    
    public LLMProviderController(
        ILLMProviderManagementService service,
        ILogger<LLMProviderController> logger)
    {
        _service = service;
        _logger = logger;
    }
    
    /// <summary>
    /// 获取所有LLM供应商
    /// 注意：此接口允许所有已认证用户访问（普通用户也可选择供应商）
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LLMProviderResponse>), 200)]
    [ProducesResponseType(401)]
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
    [ProducesResponseType(typeof(LLMProviderResponse), 200)]
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
    [ProducesResponseType(typeof(LLMProviderResponse), 200)]
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
    [ProducesResponseType(typeof(LLMProviderResponse), 200)]
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
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(LLMProviderResponse), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateProvider(
        [FromBody] CreateLLMProviderRequest request,
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
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(LLMProviderResponse), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UpdateProvider(
        Guid id,
        [FromBody] UpdateLLMProviderRequest request,
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
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
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
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
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
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
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
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(ValidateProviderResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> TestConnection(
        [FromBody] CreateLLMProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.TestProviderConnectionAsync(
            request,
            cancellationToken);
        return Ok(result);
    }
}
