using DevNexus.Domain.Abstractions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// 模型定价管理 API
/// </summary>
[ApiController]
[Route("api/v1/model-pricing")]
[Authorize]
public class ModelPricingController : ControllerBase
{
    private readonly IModelPricingService _pricingService;
    private readonly ILogger<ModelPricingController> _logger;

    public ModelPricingController(
        IModelPricingService pricingService,
        ILogger<ModelPricingController> logger)
    {
        _pricingService = pricingService;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有模型定价配置
    /// </summary>
    /// <returns>定价配置列表</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ModelPricingResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ModelPricingResponse>>> GetAllPricings(
        CancellationToken cancellationToken = default)
    {
        var pricings = await _pricingService.GetAllPricingsAsync(cancellationToken);
        return Ok(pricings);
    }

    /// <summary>
    /// 根据 ID 获取模型定价配置
    /// </summary>
    /// <param name="id">定价配置 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>定价配置</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ModelPricingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ModelPricingResponse>> GetPricingById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var pricing = await _pricingService.GetPricingByIdAsync(id, cancellationToken);
        
        if (pricing == null)
        {
            return NotFound(new { message = $"Pricing with ID {id} not found." });
        }

        return Ok(pricing);
    }

    /// <summary>
    /// 创建模型定价配置（仅管理员）
    /// </summary>
    /// <param name="request">创建请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的定价配置</returns>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(ModelPricingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ModelPricingResponse>> CreatePricing(
        [FromBody] CreateModelPricingRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pricing = await _pricingService.CreatePricingAsync(request, cancellationToken);
            
            _logger.LogInformation(
                "[ModelPricing] Created pricing for provider {ProviderId} | ProviderType={ProviderType}",
                request.ProviderId,
                request.ProviderType);

            return CreatedAtAction(
                nameof(GetPricingById),
                new { id = pricing.Id },
                pricing);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// 更新模型定价配置（仅管理员）
    /// </summary>
    /// <param name="id">定价配置 ID</param>
    /// <param name="request">更新请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的定价配置</returns>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(ModelPricingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ModelPricingResponse>> UpdatePricing(
        Guid id,
        [FromBody] UpdateModelPricingRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pricing = await _pricingService.UpdatePricingAsync(id, request, cancellationToken);

            _logger.LogInformation(
                "[ModelPricing] Updated pricing {Id}",
                id);

            return Ok(pricing);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// 删除模型定价配置（仅管理员）
    /// </summary>
    /// <param name="id">定价配置 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeletePricing(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _pricingService.DeletePricingAsync(id, cancellationToken);

        if (!deleted)
        {
            return NotFound(new { message = $"Pricing with ID {id} not found." });
        }

        _logger.LogInformation(
            "[ModelPricing] Deleted pricing {Id}",
            id);

        return NoContent();
    }
}
