
using BidEngine.Services;
using BidEngine.Services.Interfaces;
using BidEngine.Shared;
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
    private readonly IExperimentService _experimentService;
    private readonly IExperimentEventLogger _experimentEventLogger;
    private readonly IExperimentContextAccessor _experimentContextAccessor;
    private readonly IAdEventService _adEventService;
    private readonly ILogger<BidController> _logger;

    //prometheus metrics
    private static readonly Counter BidRequestsTotal = 
        Metrics.CreateCounter("bid_requests_total", 
        "Total bid requests received",
        labelNames: new[] {"status"});

    private static readonly Histogram BidLatencySeconds = Metrics
        .CreateHistogram("bid_latency_seconds", "Bid processing latency in seconds");

    public BidController(
        BidSelector bidSelector,
        BudgetService budgetService,
        IExperimentService experimentService,
        IExperimentEventLogger experimentEventLogger,
        IExperimentContextAccessor experimentContextAccessor,
        IAdEventService adEventService,
        ILogger<BidController> logger)
    {
        _bidSelector = bidSelector;
        _budgetService = budgetService;
        _experimentService = experimentService;
        _experimentEventLogger = experimentEventLogger;
        _experimentContextAccessor = experimentContextAccessor;
        _adEventService = adEventService;
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

            var assignment = await _experimentService.AssignVariationAsync(request);
            _experimentContextAccessor.CurrentAssignment = assignment;
            _experimentEventLogger.LogExposure(assignment, request, assignment?.IsAssigned == true ? "assigned" : "control");

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

            // Record the impression event for attribution and analytics.
            var impressionEvent = new AdImpressionEvent
            {
                CampaignId = winningBid.CampaignId,
                AdId = winningBid.AdId,
                UserId = request.UserId,
                PlacementId = request.PlacementId,
                RequestId = Guid.NewGuid().ToString(),
                BidPrice = winningBid.BidPrice,
                ImpressionValue = winningBid.BidPrice / 1000m,
                ExperimentId = assignment?.ExperimentId,
                VariationId = assignment?.VariationId,
                TimestampUtc = DateTime.UtcNow
            };

            await _adEventService.PublishImpressionAsync(impressionEvent);

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
    public async Task<ActionResult<string>> User_Click_Event([FromQuery] Guid? campaignId, [FromQuery] Guid? adId, [FromQuery] string? userId)
    {
        if (campaignId == null || adId == null || string.IsNullOrEmpty(userId))
        {
            return BadRequest("campaignId, adId, and userId are required");
        }

        _logger.LogInformation("User clicked on the ad: campaign={CampaignId} ad={AdId} user={UserId}", campaignId, adId, userId);

        var clickEvent = new AdClickEvent
        {
            CampaignId = campaignId.Value,
            AdId = adId.Value,
            UserId = userId,
            PlacementId = Request.Query["placementId"].ToString() ?? string.Empty,
            RequestId = Guid.NewGuid().ToString(),
            ClickValue = 0m,
            SessionId = Request.Headers["X-Session-Id"].ToString(),
            TimestampUtc = DateTime.UtcNow
        };

        await _adEventService.PublishClickAsync(clickEvent);
        return Ok("Click recorded");
    }

    
}

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly VideoEmbeddingService _videoEmbeddingService;

    public AdminController(VideoEmbeddingService videoEmbeddingService)
    {
        _videoEmbeddingService = videoEmbeddingService;
    }

    [HttpPost("seed-vectors")]
    public async Task<IActionResult> SeedVectors()
    {
        await _videoEmbeddingService.GenerateEmbeddingsForAllVideos();
        return Ok("The vectorization has been completed.");
    }

    [HttpPost("seed-vectors-with-debugging")]
    public async Task<IActionResult> SeedVectorsWithDebug()
    {
        await _videoEmbeddingService.GenerateEmbeddingsForAllVideosWithDebugging();
        return Ok("The vectorization has been completed.");
    }

    [HttpPost("seed-vector-ads")]
    public async Task<IActionResult> SeedVectorsAds()
    {
        await _videoEmbeddingService.GenerateEmbeddingsForAllAds();
        return Ok("The vectorization for all ads has been completed.");
    }
}