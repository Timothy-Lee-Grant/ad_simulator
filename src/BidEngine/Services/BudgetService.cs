
using BidEngine.Data;
using BidEngine.Models;
using StackExchange.Redis;
using Microsoft.EntityFrameworkCore;


namespace BidEngine.Services;

/// <summary>
/// Manages campaign budget tracking and deduction.
/// 
/// Why separate service?
/// - Budget tracking is critical and must be accurate
/// - Multiple services need to call this
/// - Needs to be tested independently
/// </summary>
/// 
public class BudgetService
{
    private readonly AppDbContext _dbContext;
    private readonly IDatabase _redis;
    private readonly ILogger<BudgetService> _logger;

    public BudgetService(AppDbContext appDbContext, IConnectionMultiplexer database, ILogger<BudgetService> logger)
    {
        _dbContext = appDbContext;
        _redis = database.GetDatabase();
        _logger = logger;
    }

    /// <summary>
    /// Deduct spend from campaign budget after an impression is served.
    /// 
    /// Calculation:
    /// - CPM = Cost Per Mille (per 1000 impressions)
    /// - Cost per impression = CPM / 1000
    /// - Example: $2.50 CPM means $0.0025 per impression
    /// </summary>
    /// Tim Grant - I don't think this is acutally going to work
    /// if you notice, it doesn't seem to even be changing the acual redis cahse.
    /// I don't think it is acutally invalidating any data.
    /// Also, doesn't this imply that each time we show an id we then invalidate the campaign
    /// that does not seem to be a correct thought process. I would think that we would need to
    /// invalidate only if the conditions after showing the add are now invalid.
    public async Task<bool> DeductBudgetAsync(Guid campaignId, decimal cpmBid)
    {
        try
        {
            var costPerImpression = cpmBid / 1000m;

            var campaign = await _dbContext.Campaigns
                .FindAsync(campaignId);
            if(campaign == null)
            {
                _logger.LogError("Campaign {CampaignId} not found", campaignId);
                return false;
            }

            campaign.SpentToday += costPerImpression;
            campaign.LifetimeSpent += costPerImpression;
            campaign.UpdatedAt = DateTime.UtcNow;

            if(campaign.SpentToday > campaign.DailyBudget)
            {
                _logger.LogWarning(
                    "Campaign {CampaignId} exceeded daily budget: {Spent}/{Budget}",
                    campaignId,
                    campaign.SpentToday,
                    campaign.DailyBudget
                );
                //we revert the changes
                campaign.SpentToday -= costPerImpression;
                campaign.LifetimeSpent -= costPerImpression;
                return false;
            }

            if(campaign.LifetimeBudget != null
                && campaign.LifetimeSpent > 
                campaign.LifetimeBudget.Value)
            {
                _logger.LogWarning(
                    "Campaign {CampaignId} exceeded lifetime budget: {Spent}/{Budget}",
                    campaignId,
                    campaign.LifetimeSpent,
                    campaign.LifetimeBudget.Value
                );
                // Revert the change
                campaign.SpentToday -= costPerImpression;
                campaign.LifetimeSpent -= costPerImpression;
                return false;
            }

            await _dbContext.SaveChangesAsync();

            var cashe = (CampaignCache?)
            _dbContext.GetType().Assembly
                .GetType("BidEngine.Services.CampaignCache");

            _logger.LogInformation(
                "Deducted ${Cost} from campaign {CampaignId} (new total: ${Total})",
                costPerImpression,
                campaignId,
                campaign.SpentToday
            );
            
            return true;
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error deducting budget for campaign {CampaignId}", campaignId);
            return false;
        }
    }

    /// <summary>
    /// Reset daily budget spent counter at end of day
    /// Call this daily (e.g., via scheduled job)
    /// </summary>
    public async Task ResetDailyBudgetAsync()
    {
        try
        {
            var campaigns = await _dbContext.
                Campaigns.ToListAsync();
            
            foreach(var campaign in campaigns)
            {
                campaign.SpentToday = 0;
                campaign.UpdatedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Reset daily budget for {Count} campaigns", campaigns.Count);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error resetting daily budgets");
        }
    }
}