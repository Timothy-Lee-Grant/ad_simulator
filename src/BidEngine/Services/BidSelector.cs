
using BidEngine.Shared;

namespace BidEngine.Services;

/// <summary>
/// Core bidding algorithm - selects winning campaign using pluggable strategies.
/// 
/// Uses the Strategy pattern to allow different bidding algorithms:
/// - HighestCpmStrategy: Selects highest CPM bid
/// - SemanticOnlyStrategy: Selects based on semantic relevance
/// - HybridWeightedStrategy: Combines CPM and semantic scoring
/// </summary>
public class BidSelector
{
    private readonly IBiddingStrategy _strategy;
    private readonly ILogger<BidSelector> _logger;

    public BidSelector(IBiddingStrategy strategy, ILogger<BidSelector> logger)
    {
        _strategy = strategy;
        _logger = logger;
    }

    public async Task<BidResponse?> SelectWinningBidAsync(BidRequest request)
    {
        _logger.LogInformation(
            "Processing bid request for user {UserId} using strategy {Strategy}",
            request.UserId,
            _strategy.GetType().Name
        );

        return await _strategy.SelectWinningBidAsync(request);
    }
}