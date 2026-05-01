using BidEngine.Services.Interfaces;
using BidEngine.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BidEngine.Controllers;

[ApiController]
[Route("api/admin/experiments")]
[Authorize(Policy = "AdminOnly")]
public class AdminExperimentsController : ControllerBase
{
    private readonly IExperimentService _experimentService;

    public AdminExperimentsController(IExperimentService experimentService)
    {
        _experimentService = experimentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetExperiments()
    {
        var experiments = await _experimentService.GetActiveExperimentsAsync();
        return Ok(experiments);
    }

    [HttpGet("{experimentId}")]
    public async Task<IActionResult> GetExperiment(string experimentId)
    {
        var experiment = await _experimentService.GetExperimentAsync(experimentId);
        return experiment == null ? NotFound(new { Message = "Experiment not found" }) : Ok(experiment);
    }

    [HttpGet("{experimentId}/assignments")]
    public async Task<IActionResult> GetAssignment([FromRoute] string experimentId, [FromQuery] string userId, [FromQuery] string placementId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(placementId))
        {
            return BadRequest(new { Message = "userId and placementId are required" });
        }

        var experiment = await _experimentService.GetExperimentAsync(experimentId);
        if (experiment == null)
        {
            return NotFound(new { Message = "Experiment not found" });
        }

        var request = new BidRequest { UserId = userId, PlacementId = placementId };
        var assignment = await _experimentService.AssignVariationAsync(request);
        return Ok(assignment);
    }
}
