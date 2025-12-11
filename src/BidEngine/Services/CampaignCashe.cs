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
        //this entire thing simplifies to one sql statement. It will look like
        /*
        SELECT c.Id, c.Name, a.Id, a.CampaignId, a.Title
        FROM Campaigns c
        LEFT JOIN Ads a ON c.Id = a.CampaignId
        WHERE c.Id = 1;

        */
        var dbCampaign = await _dbContext.Campaigns
            .Include(c => c.Ads)
            .Include(c => c.TargetingRules)
            .FirstOrDefaultAsync(c => c.Id == campaignId);

        if(dbCampaign != null)
        {
            //what does serialize acutally do? does it return a string or a json obect??
            var json = JsonSerializer.Serialize(dbCampaign);
            await _redis.StringSetAsync(cacheKey, json, TimeSpan.FromSeconds(CacheTtlSeconds));
        }
        
        return dbCampaign;
    }

    /// <summary>
    /// Get all active campaigns, checking cache first
    /// </summary>
    public async Task<List<Campaign>> GetActiveCampaignsAsync()
    {
        var casheKey = "campaigns::active::all";

        var cashed = await _redis.
            StringGetAsync(casheKey);

        if (cashed.HasValue)
        {
            _logger.LogInformation("Cache hit for active campaigns");
            var res = JsonSerializer.Deserialize<List<Campaign>>(cashed.ToString());
            return res ?? new();
        }

        _logger.LogInformation("Cache miss for active campaigns, querying database");
        var res = await _dbContext.Campaign
            .Include(c=>c.Ads)
            .Include(c=>c.TargetingRule)
            .Where(c => c.Status == "active")
            .ToListAsync();

        var json = JsonSerializer.Serialize(res);
        await _redis.StringSetAsync(casheKey, json, TimeSpan.FromSeconds(CacheTtlSeconds));
        return res;
        //I guess my way of doing this is not accurate....
        /*
        var cashedActiveCampaigns = await _redis.SomeMethod(campaign => campaign.status == "active");
        var sqlActiveCampaigns = await _dbContext.Campaign
            .find(c => c.status == "active");

        return cashedActiveCampaigns + sqlActiveCampaigns;
        */
    }
    
}
