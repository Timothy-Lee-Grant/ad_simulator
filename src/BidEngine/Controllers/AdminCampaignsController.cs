using BidEngine.Services.Interfaces;
using BidEngine.Shared.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BidEngine.Controllers;

[ApiController]
[Route("api/admin/campaigns")]
[Authorize(Policy = "AdminOnly")]
public class AdminCampaignsController : ControllerBase
{
    private readonly ICampaignManagementService _campaignService;
    private readonly IValidator<CreateCampaignRequest> _createCampaignValidator;
    private readonly IValidator<UpdateCampaignRequest> _updateCampaignValidator;

    public AdminCampaignsController(
        ICampaignManagementService campaignService,
        IValidator<CreateCampaignRequest> createCampaignValidator,
        IValidator<UpdateCampaignRequest> updateCampaignValidator)
    {
        _campaignService = campaignService;
        _createCampaignValidator = createCampaignValidator;
        _updateCampaignValidator = updateCampaignValidator;
    }

    [HttpGet]
    public async Task<IActionResult> GetCampaigns([FromQuery] string? status, [FromQuery] Guid? advertiserId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _campaignService.ListCampaignsAsync(page, pageSize, status, advertiserId);
        return Ok(result);
    }

    [HttpGet("{campaignId}")]
    public async Task<IActionResult> GetCampaign(Guid campaignId)
    {
        var campaign = await _campaignService.GetCampaignAsync(campaignId);
        return campaign == null ? NotFound(new { Message = "Campaign not found" }) : Ok(campaign);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCampaign([FromBody] CreateCampaignRequest request)
    {
        var validation = await _createCampaignValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(new { Errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var campaign = await _campaignService.CreateCampaignAsync(request, userId.Value);
        return CreatedAtAction(nameof(GetCampaign), new { campaignId = campaign.Id }, campaign);
    }

    [HttpPut("{campaignId}")]
    public async Task<IActionResult> UpdateCampaign(Guid campaignId, [FromBody] UpdateCampaignRequest request)
    {
        var validation = await _updateCampaignValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(new { Errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var campaign = await _campaignService.UpdateCampaignAsync(campaignId, request, userId.Value);
        return campaign == null ? NotFound(new { Message = "Campaign not found" }) : Ok(campaign);
    }

    [HttpDelete("{campaignId}")]
    public async Task<IActionResult> DeleteCampaign(Guid campaignId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var deleted = await _campaignService.DeleteCampaignAsync(campaignId, userId.Value);
        return deleted ? NoContent() : NotFound(new { Message = "Campaign not found" });
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId) ? userId : null;
    }
}
