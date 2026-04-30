using BidEngine.Data;
using BidEngine.Shared;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BidEngine.Services;

public class CampaignReadCacheService
{
    private readonly IDatabase _redis;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<CampaignReadCacheService> _logger;
    private const int CacheTtlSeconds = 300;

    public CampaignReadCacheService(IConnectionMultiplexer connectionMultiplexer, AppDbContext dbContext, ILogger<CampaignReadCacheService> logger)
    {
        _redis = connectionMultiplexer.GetDatabase();
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Campaign?> GetCampaignAsync(Guid campaignId)
    {
        var cacheKey = $"campaign::{campaignId}";
        var cached = await _redis.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            _logger.LogInformation("Cache hit for campaign {CampaignId}", campaignId);
            return JsonSerializer.Deserialize<Campaign>(cached.ToString());
        }

        _logger.LogInformation("Cache miss for campaign {CampaignId}, querying database", campaignId);
        var campaign = await _dbContext.Campaigns
            .Include(c => c.Ads)
            .Include(c => c.TargetingRules)
            .FirstOrDefaultAsync(c => c.Id == campaignId);

        if (campaign != null)
        {
            var options = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };

            var json = JsonSerializer.Serialize(campaign, options);
            await _redis.StringSetAsync(cacheKey, json, TimeSpan.FromSeconds(CacheTtlSeconds));
        }

        return campaign;
    }

    public async Task<List<Campaign>> GetActiveCampaignsAsync()
    {
        var cacheKey = "campaigns::active::all";
        var cached = await _redis.StringGetAsync(cacheKey);

        if (cached.HasValue)
        {
            _logger.LogInformation("Cache hit for active campaigns");
            return JsonSerializer.Deserialize<List<Campaign>>(cached.ToString()) ?? new List<Campaign>();
        }

        _logger.LogInformation("Cache miss for active campaigns, querying database");
        var campaigns = await _dbContext.Campaigns
            .Include(c => c.Ads)
            .Include(c => c.TargetingRules)
            .Where(c => c.Status == "active")
            .ToListAsync();

        var options = new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };

        var json = JsonSerializer.Serialize(campaigns, options);
        await _redis.StringSetAsync(cacheKey, json, TimeSpan.FromSeconds(CacheTtlSeconds));

        return campaigns;
    }

    public async Task InvalidateCampaignAsync(Guid campaignId)
    {
        var cacheKey = $"campaign::{campaignId}";
        await _redis.KeyDeleteAsync(cacheKey);
        await _redis.KeyDeleteAsync("campaigns::active::all");
        _logger.LogInformation("Invalidated cache for campaign {CampaignId}", campaignId);
    }
}
