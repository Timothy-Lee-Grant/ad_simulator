using BidEngine.Services.Interfaces;
using BidEngine.Shared.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BidEngine.Controllers;

[ApiController]
[Route("api/admin/ads")]
[Authorize(Policy = "AdminOnly")]
public class AdminAdsController : ControllerBase
{
    private readonly ICampaignManagementService _campaignService;
    private readonly IValidator<CreateAdRequest> _createAdValidator;
    private readonly IValidator<UpdateAdRequest> _updateAdValidator;

    public AdminAdsController(
        ICampaignManagementService campaignService,
        IValidator<CreateAdRequest> createAdValidator,
        IValidator<UpdateAdRequest> updateAdValidator)
    {
        _campaignService = campaignService;
        _createAdValidator = createAdValidator;
        _updateAdValidator = updateAdValidator;
    }

    [HttpPost("campaign/{campaignId}")]
    public async Task<IActionResult> CreateAd(Guid campaignId, [FromBody] CreateAdRequest request)
    {
        var validation = await _createAdValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(new { Errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var ad = await _campaignService.CreateAdAsync(campaignId, request, userId.Value);
        return CreatedAtAction(nameof(GetAd), new { adId = ad.Id }, ad);
    }

    [HttpGet("{adId}")]
    public async Task<IActionResult> GetAd(Guid adId)
    {
        // Admin ad retrieval can reuse campaign query indirectly if needed.
        var ad = await _campaignService.GetCampaignAsync(adId);
        return NotFound(new { Message = "Ad retrieval endpoint not implemented" });
    }

    [HttpPut("{adId}")]
    public async Task<IActionResult> UpdateAd(Guid adId, [FromBody] UpdateAdRequest request)
    {
        var validation = await _updateAdValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(new { Errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var ad = await _campaignService.UpdateAdAsync(adId, request, userId.Value);
        return ad == null ? NotFound(new { Message = "Ad not found" }) : Ok(ad);
    }

    [HttpDelete("{adId}")]
    public async Task<IActionResult> DeleteAd(Guid adId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var deleted = await _campaignService.DeleteAdAsync(adId, userId.Value);
        return deleted ? NoContent() : NotFound(new { Message = "Ad not found" });
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId) ? userId : null;
    }
}
