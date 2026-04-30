using BidEngine.Shared;

namespace BidEngine.Services;

/// <summary>
/// Interface for pluggable bidding strategies.
/// Each strategy implements its own logic for selecting a winning bid.
/// </summary>
public interface IBiddingStrategy
{
    Task<BidResponse?> SelectWinningBidAsync(BidRequest request);
}
