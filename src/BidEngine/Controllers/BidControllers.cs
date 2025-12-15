
using BidEngine.Models;
using BidEngine.Services;
using Microsoft.AspNetCore.Mvc;
using Prometheus;

namespace BidEngine.Controllers;

/// <summary>
/// REST API endpoint for bidding
/// Receives bid requests from Ad Server and returns winning campaign
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class BidController : ControllerBase
{
    private readonly BidSelector _bidSelector;
    private readonly BudgetService _budgetService;
    private readonly ILogger<BidController> _logger;

    //prometheus metrics
    private static readonly Counter BidRequestsTotal = 
        Metrics.CreateCounter("bid_requests_total", 
        "Total bid requests received",
        labelNames: new[] {"status"});

    private static readonly Histogram BidLatencySeconds = Metrics
        .CreateHistogram("bid_latency_seconds", "Bid processing latency in seconds");

    public BidController(BidSelector bidSelector, BudgetService budgetService, ILogger<BidController> logger)
    {
        _bidSelector = bidSelector;
        _budgetService = budgetService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/bid
    /// Evaluates all active campaigns and returns winning bid
    /// 
    /// SLO: p95 latency < 50ms
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<BidResponse>> EvaluateBidsAsync(
        [FromBody] BidRequest request)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            if(string.IsNullOrEmpty(request.UserId)
                || string.IsNullOrEmpty(request.PlacementId))
            {
                BidRequestsTotal.WithLabels("invalid").Inc();
                return BadRequest("UserId and PlacementId are required");
            }

            var winningBid = await _bidSelector.SelectWinningBidAsync(request);
        
            if(winningBid == null)
            {
                // No eligible campaign - return 204 No Content
                _logger.LogInformation(
                    "No winning bid for user {UserId} on placement {PlacementId}",
                    request.UserId,
                    request.PlacementId
                );
                BidRequestsTotal.WithLabels("no_bid").Inc();
                return NoContent();
            }

            //deduct budget for winning campaign
            var budgetDeducted = await _budgetService.DeductBudgetAsync(winningBid.CampainId, winningBid.BidPrice);

            if(!budgetDeducted)
            {
                _logger.LogWarning(
                    "Failed to deduct budget for campaign {CampaignId}",
                    winningBid.CampaignId
                );
                BidRequestsTotal.WithLabels("budget_error").Inc();
                return StatusCode(503, "Service temporarily unavailable");
            }

            BidRequestsTotal.WithLabels("success").Inc();

            // Tim Grant - make sure we learn how to acutally see this latency histogram in prometheus later
            var latency = (DateTime.UtcNow - startTime).TotalSeconds;
            BidLatencySeconds.Observe(latency);

            _logger.LogInformation(
                "Bid decision made in {LatencyMs}ms for campaign {CampaignId}",
                (DateTime.UtcNow - startTime).TotalMilliseconds,
                winningBid.CampaignId
            );

            return Ok(winningBid);
        }
        catch(Exception ex)
        {
            BidRequestsTotal.WithLabels("error").Inc();
            _logger.LogError(ex, "Error evaluating bids");
            return StatusCode(500, "Internal server error");
        }
    }
    
}