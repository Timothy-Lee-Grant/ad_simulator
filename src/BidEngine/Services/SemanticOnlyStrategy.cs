using BidEngine.Shared;

namespace BidEngine.Services;

/// <summary>
/// Bidding strategy that selects ads based on semantic similarity to video content.
/// Uses vector embeddings for relevance matching.
/// </summary>
public class SemanticOnlyStrategy : IBiddingStrategy
{
    private readonly VideoEmbeddingService _embeddingService;
    private readonly SemanticQueryService _semanticService;
    private readonly ILogger<SemanticOnlyStrategy> _logger;

    public SemanticOnlyStrategy(
        VideoEmbeddingService embeddingService,
        SemanticQueryService semanticService,
        ILogger<SemanticOnlyStrategy> logger)
    {
        _embeddingService = embeddingService;
        _semanticService = semanticService;
        _logger = logger;
    }

    public async Task<BidResponse?> SelectWinningBidAsync(BidRequest request)
    {
        _logger.LogInformation(
            "Evaluating bids using Semantic Only strategy for user {UserId}",
            request.UserId
        );

        if (request.VideoId == null)
        {
            _logger.LogWarning("Semantic strategy requires VideoId, but none provided");
            return null;
        }

        var videoVector = await _embeddingService.FindVectorFromVideoId(request.VideoId.Value);
        if (videoVector == null)
        {
            _logger.LogWarning("No vector found for video {VideoId}", request.VideoId.Value);
            return null;
        }

        var topAds = await _semanticService.PerformSemanticSearchForTop3Ads(videoVector);
        if (topAds == null || !topAds.Any())
        {
            return null;
        }

        var winner = topAds.First();
        return new BidResponse
        {
            AdId = winner.Id,
            AdContent = new AdContent
            {
                Title = winner.Title,
                ImageUrl = winner.ImageUrl,
                RedirectUrl = winner.RedirectUrl,
                Description = winner.Description
            },
            CampaignId = winner.CampaignId,
            BidPrice = 3, // Hardcoded for now - should be configurable
            Confidence = 8
        };
    }
}
