using BidEngine.Services.Interfaces;
using BidEngine.Shared.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BidEngine.Controllers;

[ApiController]
[Route("api/admin/targeting")]
[Authorize(Policy = "AdminOnly")]
public class AdminTargetingController : ControllerBase
{
    private readonly ICampaignManagementService _campaignService;
    private readonly IValidator<CreateTargetingRuleRequest> _createTargetingValidator;
    private readonly IValidator<UpdateTargetingRuleRequest> _updateTargetingValidator;

    public AdminTargetingController(
        ICampaignManagementService campaignService,
        IValidator<CreateTargetingRuleRequest> createTargetingValidator,
        IValidator<UpdateTargetingRuleRequest> updateTargetingValidator)
    {
        _campaignService = campaignService;
        _createTargetingValidator = createTargetingValidator;
        _updateTargetingValidator = updateTargetingValidator;
    }

    [HttpPost("campaign/{campaignId}")]
    public async Task<IActionResult> CreateTargetingRule(Guid campaignId, [FromBody] CreateTargetingRuleRequest request)
    {
        var validation = await _createTargetingValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(new { Errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var rule = await _campaignService.CreateTargetingRuleAsync(campaignId, request, userId.Value);
        return CreatedAtAction(nameof(GetTargetingRule), new { ruleId = rule.Id }, rule);
    }

    [HttpGet("{ruleId}")]
    public async Task<IActionResult> GetTargetingRule(Guid ruleId)
    {
        // For now, return not implemented; could implement if needed
        return NotFound(new { Message = "Targeting rule retrieval endpoint not implemented" });
    }

    [HttpPut("{ruleId}")]
    public async Task<IActionResult> UpdateTargetingRule(Guid ruleId, [FromBody] UpdateTargetingRuleRequest request)
    {
        var validation = await _updateTargetingValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(new { Errors = validation.Errors.Select(e => e.ErrorMessage) });
        }

        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var rule = await _campaignService.UpdateTargetingRuleAsync(ruleId, request, userId.Value);
        return rule == null ? NotFound(new { Message = "Targeting rule not found" }) : Ok(rule);
    }

    [HttpDelete("{ruleId}")]
    public async Task<IActionResult> DeleteTargetingRule(Guid ruleId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var deleted = await _campaignService.DeleteTargetingRuleAsync(ruleId, userId.Value);
        return deleted ? NoContent() : NotFound(new { Message = "Targeting rule not found" });
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId) ? userId : null;
    }
}