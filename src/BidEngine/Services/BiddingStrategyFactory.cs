using Microsoft.Extensions.Options;

namespace BidEngine.Services;

/// <summary>
/// Configuration options for bidding strategy selection.
/// </summary>
public class BiddingStrategyOptions
{
    public string Strategy { get; set; } = "HighestCpm"; // Default strategy
}

/// <summary>
/// Factory for creating bidding strategies based on configuration.
/// </summary>
public class BiddingStrategyFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly BiddingStrategyOptions _options;

    public BiddingStrategyFactory(IServiceProvider serviceProvider, IOptions<BiddingStrategyOptions> options)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
    }

    public IBiddingStrategy CreateStrategy()
    {
        return _options.Strategy.ToLowerInvariant() switch
        {
            "highestcpm" => _serviceProvider.GetRequiredService<HighestCpmStrategy>(),
            "semanticonly" => _serviceProvider.GetRequiredService<SemanticOnlyStrategy>(),
            "hybridweighted" => _serviceProvider.GetRequiredService<HybridWeightedStrategy>(),
            _ => _serviceProvider.GetRequiredService<HighestCpmStrategy>() // Default fallback
        };
    }
}
