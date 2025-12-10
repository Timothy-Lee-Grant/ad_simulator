using BidEngine.Data;
using BidEngine.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace BidEngine.Services;

public class CampaignCashe
{
    private readonly IDatabase _redis;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<CampaignCache> _logger;
    private const int CacheTtlSeconds = 300; // 5 minutes

    public void CampaignCache(IConnectionMultiplexer db, AppDbContext context, ILogger<CampaignCashe> logger)
    {
        _redis = db.GetDatabase();
        _dbContext = context;
        _logger = logger;
    }

    public async Task<Campaign?> GetCampaignAsync(Guid campaignId)
    {

        var cacheKey = $"campaign::{campaignId}";
        
        // Step 1: Try to get from Redis cache
        var cached = await _redis.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            _logger.LogInformation("Cache hit for campaign {CampaignId}", campaignId);
            var campaign = JsonSerializer.Deserialize<Campaign>(cached.ToString());
            return campaign;
        }

        //if no cashe hit. need to find in sql, get campaign, store it in redis, return found object
        _logger.LogInformation("Cache miss for campaign {CampaignId}, querying database", campaignId);
        var dbCampaign = await _dbContext.Campaigns
            .Include(c)
    }
    
}
