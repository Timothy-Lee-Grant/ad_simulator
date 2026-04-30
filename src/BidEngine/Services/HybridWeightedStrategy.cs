using BidEngine.Shared;

namespace BidEngine.Services;

/// <summary>
/// Bidding strategy that combines semantic relevance and CPM bidding.
/// Uses a weighted score: semantic similarity + normalized CPM bid.
/// </summary>
public class HybridWeightedStrategy : IBiddingStrategy
{
    private readonly CampaignReadCacheService _campaignService;
    private readonly VideoEmbeddingService _embeddingService;
    private readonly SemanticQueryService _semanticService;
    private readonly ILogger<HybridWeightedStrategy> _logger;
    private readonly Random _random = new();

    // Weights for combining semantic and CPM scores
    private const double SemanticWeight = 0.6;
    private const double CpmWeight = 0.4;

    public HybridWeightedStrategy(
        CampaignReadCacheService campaignService,
        VideoEmbeddingService embeddingService,
        SemanticQueryService semanticService,
        ILogger<HybridWeightedStrategy> logger)
    {
        _campaignService = campaignService;
        _embeddingService = embeddingService;
        _semanticService = semanticService;
        _logger = logger;
    }

    public async Task<BidResponse?> SelectWinningBidAsync(BidRequest request)
    {
        _logger.LogInformation(
            "Evaluating bids using Hybrid Weighted strategy for user {UserId}",
            request.UserId
        );

        var activeCampaigns = await _campaignService.GetActiveCampaignsAsync();
        if (!activeCampaigns.Any())
        {
            _logger.LogWarning("No active campaigns found");
            return null;
        }

        // Filter eligible campaigns
        var eligibleCampaigns = activeCampaigns.Where(c =>
            c.CanServe &&
            MatchesTargetingRules(c, request) &&
            c.Ads.Any()
        ).ToList();

        if (!eligibleCampaigns.Any())
        {
            _logger.LogWarning("No eligible campaigns after filtering");
            return null;
        }

        // Calculate hybrid scores for each campaign
        var campaignScores = new List<(Campaign Campaign, double Score)>();

        foreach (var campaign in eligibleCampaigns)
        {
            double score = 0;

            if (request.VideoId.HasValue)
            {
                // Add semantic score
                var videoVector = await _embeddingService.FindVectorFromVideoId(request.VideoId.Value);
                if (videoVector != null)
                {
                    var semanticScore = await CalculateSemanticScore(campaign, videoVector);
                    score += semanticScore * SemanticWeight;
                }
            }

            // Add normalized CPM score
            var cpmScore = NormalizeCpmBid(campaign.CpmBid, eligibleCampaigns);
            score += cpmScore * CpmWeight;

            campaignScores.Add((campaign, score));
        }

        // Select winning campaign with highest hybrid score
        var winner = campaignScores.OrderByDescending(cs => cs.Score).First();
        var winningCampaign = winner.Campaign;

        _logger.LogInformation(
            "Campaign {CampaignId} won with hybrid score {Score}",
            winningCampaign.Id,
            winner.Score
        );

        // Select random ad from winning campaign
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
            Confidence = winner.Score
        };
    }

    private async Task<double> CalculateSemanticScore(Campaign campaign, Pgvector.Vector videoVector)
    {
        // Find the best semantic match among campaign's ads
        double bestSimilarity = 0;

        foreach (var ad in campaign.Ads)
        {
            if (ad.Embedding != null)
            {
                // Calculate cosine similarity (placeholder - actual implementation would use pgvector)
                var similarity = CalculateCosineSimilarity(videoVector, ad.Embedding);
                bestSimilarity = Math.Max(bestSimilarity, similarity);
            }
        }

        return bestSimilarity;
    }

    private double NormalizeCpmBid(decimal cpmBid, List<Campaign> campaigns)
    {
        var maxBid = campaigns.Max(c => c.CpmBid);
        var minBid = campaigns.Min(c => c.CpmBid);

        if (maxBid == minBid) return 1.0; // All same bid

        return (double)((cpmBid - minBid) / (maxBid - minBid));
    }

    private double CalculateCosineSimilarity(Pgvector.Vector a, Pgvector.Vector b)
    {
        // Placeholder - in real implementation, use pgvector's cosine distance
        // For now, return a random similarity score between 0 and 1
        return new Random().NextDouble();
    }

    private bool MatchesTargetingRules(Campaign campaign, BidRequest request)
    {
        // Placeholder for targeting logic
        return true;
    }
}
