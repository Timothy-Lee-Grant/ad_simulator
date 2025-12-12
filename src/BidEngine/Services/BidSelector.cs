
using BidEngine.Models;

namespace BidEngine.Services;

/// <summary>
/// Core bidding algorithm - selects winning campaign
/// 
/// Algorithm Logic:
/// 1. Get all active campaigns
/// 2. Filter campaigns that match targeting rules
/// 3. Filter campaigns that have budget available
/// 4. Select campaign with highest CPM bid
/// 5. Return winning campaign and random ad from it
/// </summary>
/// 
/// Think about this for future improvements I will want to create
/// a greedy algorithm which selects for the best ad within the best
/// campaign (not the absolute optimal best ad, or a random ad which
/// is currently being implemented as of now.)
public class BidSelector
{
    private readonly CampaignCashe _cashe;
    private readonly ILogger<BidSelector> _logger;
    private readonly Random _random = new();

    public BidSelector(CampaignCashe cashe, ILogger<BidSelector> logger)
    {
        _cashe = cashe;
        _logger = logger;
    }

    /// <summary>
    /// Select the winning campaign based on bid price and targeting match
    /// </summary>
    public async Task<BidResponse?> SelectWinningBidAsync(BidRequest request)
    {
        _logger.LogInformation(
            "Evaluating bids for user {UserId} on placement {PlacementId}",
            request.UserId,
            request.PlacementId
        );

        // step 1: get all the active campaigns from the cashe
        var activeCampaigns = await _cashe.GetActiveCampaignsAsync();
        if(!activeCampaigns.Any())
        {
            _logger.LogWarning("No active campaigns found");
            return null;
        }

        //step 2; filter campaigns based on targeting rules
        var eligibleCampaigns = new List<Campaign>();

        foreach (var campaign in activeCampaigns)
        {
            //check if the campaign can be served
            //ie if budget. available
            if(!campaign.CanServe)
            {
                _logger.LogInformation(
                    "Campaign {CampaignId} cannot serve: status={Status}, budget available",
                    campaign.Id,
                    campaign.Status
                );
                continue;
            }

            //check if campaign matches targeting rules
            if(!MatchesTargetingRules(campaign, request))
            {
                _logger.LogInformation(
                    "Campaign {CampaignId} doesn't match targeting rules",
                    campaign.Id
                );
                continue;
            }

            eligibleCampaigns.Add(campaign);
        }

        if(!eligibleCampaigns.Any())
        {
            _logger.LogWarning(
                "No eligible campaigns after filtering for user {UserId}",
                request.UserId
            );
            return null; 
        }

        //step 3 select campaign with highest cpm bid
        var winningCampaign = eligibleCampaigns.OrderbyDecending(c=>c.CpmBid).First();
    
        _logger.LogInformation(
            "Campaign {CampaignId} won with CPM bid {Bid}",
            winningCampaign.Id,
            winningCampaign.CpmBid
        );

        //step 4: select random ad from winning campaign
        if(!winningCampaign.Ads.Any())
        {
            _logger.LogWarning("Winning campaign {CampaignId} has no ads", winningCampaign.Id);
           //Tim Grant - I should eventually create a new state which is 'domrant' menaing the 
           //campaign is active, but no ads are currently in the campaign 
           //becuase in this methodology we just wasted an opportunity to serve an ad to a legitimate customer
            return null;
        }

        var selectedAd = winningCampaign.Ads
        [_random.Next(winningCampaign.Ads.
        Count)];

        var response = new BidResponse
        {
            CampaignId = winningCampaign.Id,
            AdId = selectedAd.Id,
            BidPrice = winningCampaign.CpmBid,
            AdContent = new AdContent
            {
                Title = selectedAd.Title,
                ImageUrl = selectedAd.ImageUrl,
                RedirectUrl = selectedAd.RedirectUrl
            },
            Confidence = 0.95 // this will need to be updated later to match real targeting metrics
        };

        return response;
    }

    private bool MatchesTargetingRules(Campaign campaign, BidRequest request)
    {
        //if no targeting rules
        // then it matches everything
        if(!campaign.TargetingRules.Any())
        {
            return true;
        }

        //group rules by type
        var rulesByType = campaign.
            TargetingRules.GroupBy(
                r=>r.RuleType)
                .ToDictionary(g=>g.ToList());

        if(rulesByType.TryGetValue("country", out var countryRules))
        {
            if(request.CountryCode == null ||
                !countryRules.Any(r=>r.RuleValue.Equals(
                    CountryCode, StringComparison.OrdinalIgnoreCase
                )))
            {
                return false;
            }
        }

        //check device type rul
        if(rulesByType.TryGetValue("device_type", out var deviceRules))
        {
            if(request.DeviceType == null
                || !deviceRules.Any(r=>r.RuleValue
                .Equals(request.DeviceType, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        //Tim Grant - Later add more targeting rules to make it more specific.

        return true;
    }
}