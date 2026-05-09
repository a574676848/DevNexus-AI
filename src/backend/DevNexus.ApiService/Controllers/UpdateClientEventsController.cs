using DevNexus.Core.Services;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// 客户端更新事件控制器。
/// </summary>
[ApiController]
[Route("api/update/events")]
public class UpdateClientEventsController : ControllerBase
{
    private readonly IUpdateClientEventService _eventService;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public UpdateClientEventsController(IUpdateClientEventService eventService)
    {
        _eventService = eventService;
    }

    /// <summary>
    /// 上报客户端更新事件。
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ReportAsync(
        [FromBody] ReportUpdateClientEventRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _eventService.ReportAsync(request, cancellationToken);
            return Accepted();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Error(ex.Message, StatusCodes.Status400BadRequest));
        }
    }
}
