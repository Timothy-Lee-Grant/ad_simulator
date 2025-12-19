
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
            var budgetDeducted = await _budgetService.DeductBudgetAsync(winningBid.CampaignId, winningBid.BidPrice);

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


    /// <summary>
    /// GET /api/bid/test
    /// Simple health check endpoint to verify the service is running
    /// </summary>
    [HttpGet("test")]
    public ActionResult<string> Test()
    {
        _logger.LogInformation("Test endpoint called");
        return Ok("BidEngine is running!");
    }

    /// <summary>
    /// GET /api/bid/test
    /// Simple health check endpoint to verify the service is running
    /// </summary>
    [HttpGet("User_Click_Event")]
    public ActionResult<string> User_Click_Event([FromQuery] Guid? campaignId, [FromQuery] Guid? adId, [FromQuery] string? userId)
    {
        _logger.LogInformation("User clicked on the ad: campaign={CampaignId} ad={AdId} user={UserId}", campaignId, adId, userId);

        // Record click metric with labels (campaign, ad)
        try
        {
            var clicks = Metrics.CreateCounter("ad_clicks_total", "Total ad clicks", new CounterConfiguration { LabelNames = new[] { "campaign", "ad" } });
            clicks.WithLabels(campaignId?.ToString() ?? "unknown", adId?.ToString() ?? "unknown").Inc();
        }
        catch
        {
            // don't let metrics errors surface to caller
        }

        // In a real system, we would validate the campaign/ad, record the click event to Kafka or DB,
        // and possibly trigger post-click logic (conversion tracking, attribution, etc).

        // Tim Grant - I will need to add functionality which deducts more money from the campaign budget when the user
        //clicks on an ad. 
        return Ok("Click recorded");
    }

    
}