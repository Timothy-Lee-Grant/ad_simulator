using BidEngine.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BidEngine.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("api/admin/metrics")]
public class AdminMetricsController : ControllerBase
{
    private readonly IAdEventService _adEventService;

    public AdminMetricsController(IAdEventService adEventService)
    {
        _adEventService = adEventService;
    }

    [HttpGet("campaigns/{campaignId}")]
    public async Task<IActionResult> GetCampaignMetrics(Guid campaignId, DateTime? from = null, DateTime? to = null)
    {
        var fromDate = from?.ToUniversalTime() ?? DateTime.UtcNow.Date.AddDays(-7);
        var toDate = to?.ToUniversalTime() ?? DateTime.UtcNow.Date;

        if (fromDate > toDate)
        {
            return BadRequest("From date must be earlier than or equal to To date.");
        }

        var metrics = await _adEventService.GetCampaignMetricsAsync(campaignId, fromDate, toDate);
        return Ok(metrics);
    }
}
