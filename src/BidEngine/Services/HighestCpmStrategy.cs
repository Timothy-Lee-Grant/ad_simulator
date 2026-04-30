using BidEngine.Shared;

namespace BidEngine.Services;

/// <summary>
/// Bidding strategy that selects the campaign with the highest CPM bid.
/// This is the traditional auction-style bidding.
/// </summary>
public class HighestCpmStrategy : IBiddingStrategy
{
    private readonly CampaignReadCacheService _campaignService;
    private readonly ILogger<HighestCpmStrategy> _logger;
    private readonly Random _random = new();

    public HighestCpmStrategy(CampaignReadCacheService campaignService, ILogger<HighestCpmStrategy> logger)
    {
        _campaignService = campaignService;
        _logger = logger;
    }

    public async Task<BidResponse?> SelectWinningBidAsync(BidRequest request)
    {
        _logger.LogInformation(
            "Evaluating bids using Highest CPM strategy for user {UserId} on placement {PlacementId}",
            request.UserId,
            request.PlacementId
        );

        // Get all active campaigns
        var activeCampaigns = await _campaignService.GetActiveCampaignsAsync();
        if (!activeCampaigns.Any())
        {
            _logger.LogWarning("No active campaigns found");
            return null;
        }

        // Filter campaigns based on targeting rules and budget
        var eligibleCampaigns = new List<Campaign>();
        foreach (var campaign in activeCampaigns)
        {
            if (!campaign.CanServe)
            {
                _logger.LogInformation(
                    "Campaign {CampaignId} cannot serve: status={Status}, budget available",
                    campaign.Id,
                    campaign.Status
                );
                continue;
            }

            if (!MatchesTargetingRules(campaign, request))
            {
                _logger.LogInformation(
                    "Campaign {CampaignId} doesn't match targeting rules",
                    campaign.Id
                );
                continue;
            }

            eligibleCampaigns.Add(campaign);
        }

        if (!eligibleCampaigns.Any())
        {
            _logger.LogWarning(
                "No eligible campaigns after filtering for user {UserId}",
                request.UserId
            );
            return null;
        }

        // Select campaign with highest CPM bid
        var winningCampaign = eligibleCampaigns.OrderByDescending(c => c.CpmBid).First();

        _logger.LogInformation(
            "Campaign {CampaignId} won with CPM bid {Bid}",
            winningCampaign.Id,
            winningCampaign.CpmBid
        );

        // Select random ad from winning campaign
        if (!winningCampaign.Ads.Any())
        {
            _logger.LogWarning("Winning campaign {CampaignId} has no ads", winningCampaign.Id);
            return null;
        }

        var selectedAd = winningCampaign.Ads[_random.Next(winningCampaign.Ads.Count)];

        return new BidResponse
        {
            CampaignId = winningCampaign.Id,
            AdId = selectedAd.Id,
            BidPrice = winningCampaign.CpmBid,
            AdContent = new AdContent
            {
                Title = selectedAd.Title,
                ImageUrl = selectedAd.ImageUrl,
                RedirectUrl = selectedAd.RedirectUrl,
                Description = selectedAd.Description
            },
            Confidence = 0.95
        };
    }

    private bool MatchesTargetingRules(Campaign campaign, BidRequest request)
    {
        // Placeholder for targeting logic - implement based on campaign.TargetingRules
        // For now, assume all campaigns match if no specific rules
        return true;
    }
}
