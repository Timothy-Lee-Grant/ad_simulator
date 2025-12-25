
using BidEngine.Shared;

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
    private readonly CampaignCache _cashe;
    private readonly ILogger<BidSelector> _logger;
    private readonly Random _random = new();

    public BidSelector(CampaignCache cashe, ILogger<BidSelector> logger)
    {
        _cashe = cashe;
        _logger = logger;
    }

    public async Task<BidResponse?> SelectWinningBidAsync(BidRequest request)
    {
        var adSelection = 0.7;
        //var res = null;
        BidResponse? res = null;

        //determine if metadata about user request exists so I can route 
        //ad selection to the sematic search or the highest bidding action
        if(request.VideoId.HasValue)
        {
            res = await SelectWinningBidBySemanticSearch(request);
        }
        else
        {
            //perform AB Testing for 'dumb highest bid auction method' 
            //for requests which do not have semantic data to compare against.
            if(adSelection>0.5)
            {
                res = await SelectWinningBidAsyncAlgorithm1(request);
            }
            else
            {
                res = await SelectWinningBidAsyncAlgorithm2(request);
            }
        }

        
        return res;
    }

    public async Task<BidResponse?> SelectWinningBidBySemanticSearch(BidRequest request)
    {
        _logger.LogInformation(
            "Using Semantic Search to Select winning advertisement to show to user: {userId}",
        request.UserId
        );
        BidResponse? result=null;



        return result;
    }

    /// <summary>
    /// Select the winning campaign based on bid price and targeting match
    /// </summary>
    public async Task<BidResponse?> SelectWinningBidAsyncAlgorithm1(BidRequest request)
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
        var winningCampaign = eligibleCampaigns.OrderByDescending(c=>c.CpmBid).First();
    
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
                    RedirectUrl = selectedAd.RedirectUrl,
                    Description = selectedAd.Description
            },
            Confidence = 0.95 // this will need to be updated later to match real targeting metrics
        };

        return response;
    }

    /*
    This function will be called by the http rest controller when the client attempts to 
    request an ad from our service.
    Inputs: request will be the body of the http resquest which comes from the user.
    Outputs: A newly created BidResponse which will signify the 'highest' priced advertisement
    which our algorithm has found. The function will fill out the fields of a BidResponse with that
    advertisement's information and return it back.
    */
    public async Task<BidResponse?> SelectWinningBidAsyncAlgorithm2(BidRequest request)
    {
        _logger.LogInformation(
            "Evaluating bids for user {UserId} on placement {PlacementId}",
            request.UserId,
            request.PlacementId
        );

        //activeCampaigns will be a List<Campaigns> which are active
        //Tim Grant - As of now we have GetActiveCampaignsAsync() declared as List<Campaigns> , but what if there are no campaigns to serve? then we should get a null value???
        var activeCampaigns = await _cashe.GetActiveCampaignsAsync();

        if(!activeCampaigns.Any())
        {
            return null;
        }

        var eligibleCampaigns = new List<Campaign>();
        foreach(var campaign in activeCampaigns)
        {
            if(!campaign.CanServe)
            {
                continue;
            }

            if(!MatchesTargetingRules(campaign, request))
            {
                continue;
            }

            eligibleCampaigns.Add(campaign);
        }

        if(!eligibleCampaigns.Any())
        {
            _logger.LogWarning("SelectWinningBidAsyncAlgorithm2 was unable to find eligable Campaign");
            return null;
        }



        //My task now is to find highest advertisement which I am hoping to select and give to the user
        //Tim Grant This will be my 'algorithm' for selection
        //could use greedy algorithm where I first select the best campaign and then select the best ad from that best campaign 
        //other idea is just do a test and select a random ad, then develop greedy later (because I would need to add multipliers to my ad dataset)
        var winningCampaign = eligibleCampaigns[_random.Next(eligibleCampaigns.Count)];

        if(!winningCampaign.Ads.Any())
        {
            _logger.LogWarning("SelectWinningBidAsyncAlgorithm2 winningCampaign has Ads count of 0");
            return null;
        }

        //Now that I have a winning campaign. I need to select a winning bid
        var winningBid = winningCampaign.Ads[_random.Next(winningCampaign.Ads.Count)];

        var response = new BidResponse
        {
            CampaignId = winningCampaign.Id,
            AdId = winningBid.Id,
            BidPrice = winningCampaign.CpmBid, // Consistent with Algorithm 1
            Confidence = 0.5,
            AdContent = new AdContent
            {
                ImageUrl = winningBid.ImageUrl,
                Title = winningBid.Title,
                    RedirectUrl = winningBid.RedirectUrl,
                    Description = winningBid.Description
            }
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
                .ToDictionary(g => g.Key, g => g.ToList());


        if(rulesByType.TryGetValue("country", out var countryRules))
        {
            if(request.CountryCode == null ||
                !countryRules.Any(r=>r.RuleValue.Equals(
                    request.CountryCode, StringComparison.OrdinalIgnoreCase //changed CountryCode -> request.CountryCode
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