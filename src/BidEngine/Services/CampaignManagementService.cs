using BidEngine.Data;
using BidEngine.Services.Interfaces;
using BidEngine.Shared;
using BidEngine.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BidEngine.Services;

public class CampaignManagementService : ICampaignManagementService
{
    private readonly AppDbContext _context;
    private readonly IAuditService _auditService;

    public CampaignManagementService(AppDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<PagedCampaignListResponse> ListCampaignsAsync(int page = 1, int pageSize = 20, string? status = null, Guid? advertiserId = null)
    {
        var query = _context.Campaigns.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status.Trim());
        }

        if (advertiserId.HasValue)
        {
            query = query.Where(c => c.AdvertiserId == advertiserId.Value);
        }

        var totalCount = await query.CountAsync();
        var campaigns = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(c => c.Ads)
            .Include(c => c.TargetingRules)
            .ToListAsync();

        return new PagedCampaignListResponse
        {
            Campaigns = campaigns.Select(ToCampaignDto),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<CampaignDto?> GetCampaignAsync(Guid campaignId)
    {
        var campaign = await _context.Campaigns
            .Include(c => c.Ads)
            .Include(c => c.TargetingRules)
            .FirstOrDefaultAsync(c => c.Id == campaignId);

        return campaign == null ? null : ToCampaignDto(campaign);
    }

    public async Task<CampaignDto> CreateCampaignAsync(CreateCampaignRequest request, Guid userId)
    {
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            AdvertiserId = request.AdvertiserId,
            Status = request.Status.Trim(),
            CpmBid = request.CpmBid,
            DailyBudget = request.DailyBudget,
            LifetimeBudget = request.LifetimeBudget,
            SpentToday = 0m,
            LifetimeSpent = 0m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Ads = request.Ads.Select(a => new Ad
            {
                Id = Guid.NewGuid(),
                Title = a.Title.Trim(),
                ImageUrl = a.ImageUrl.Trim(),
                RedirectUrl = a.RedirectUrl.Trim(),
                Description = a.Description?.Trim() ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            }).ToList(),
            TargetingRules = request.TargetingRules.Select(r => new TargetingRule
            {
                Id = Guid.NewGuid(),
                RuleType = r.RuleType.Trim(),
                RuleValue = r.RuleValue.Trim(),
                CreatedAt = DateTime.UtcNow
            }).ToList()
        };

        _context.Campaigns.Add(campaign);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(userId, "CAMPAIGN_CREATED", "Campaign", campaign.Id,
            null, System.Text.Json.JsonSerializer.Serialize(ToCampaignDto(campaign)),
            null, null, true);

        return ToCampaignDto(campaign);
    }

    public async Task<CampaignDto?> UpdateCampaignAsync(Guid campaignId, UpdateCampaignRequest request, Guid userId)
    {
        var campaign = await _context.Campaigns
            .Include(c => c.Ads)
            .Include(c => c.TargetingRules)
            .FirstOrDefaultAsync(c => c.Id == campaignId);

        if (campaign == null)
        {
            return null;
        }

        var oldSnapshot = ToCampaignDto(campaign);

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            campaign.Name = request.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            campaign.Status = request.Status.Trim();
        }

        if (request.CpmBid.HasValue)
        {
            campaign.CpmBid = request.CpmBid.Value;
        }

        if (request.DailyBudget.HasValue)
        {
            campaign.DailyBudget = request.DailyBudget.Value;
        }

        if (request.LifetimeBudget.HasValue)
        {
            campaign.LifetimeBudget = request.LifetimeBudget.Value;
        }

        campaign.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(userId, "CAMPAIGN_UPDATED", "Campaign", campaign.Id,
            System.Text.Json.JsonSerializer.Serialize(oldSnapshot),
            System.Text.Json.JsonSerializer.Serialize(ToCampaignDto(campaign)),
            null, null, true);

        return ToCampaignDto(campaign);
    }

    public async Task<bool> DeleteCampaignAsync(Guid campaignId, Guid userId)
    {
        var campaign = await _context.Campaigns.FindAsync(campaignId);
        if (campaign == null)
        {
            return false;
        }

        _context.Campaigns.Remove(campaign);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(userId, "CAMPAIGN_DELETED", "Campaign", campaignId,
            System.Text.Json.JsonSerializer.Serialize(new { campaign.Id, campaign.Name, campaign.AdvertiserId }),
            null,
            null, null, true);

        return true;
    }

    public async Task<AdDto> CreateAdAsync(Guid campaignId, CreateAdRequest request, Guid userId)
    {
        var campaign = await _context.Campaigns.FindAsync(campaignId);
        if (campaign == null)
        {
            throw new KeyNotFoundException("Campaign not found");
        }

        var ad = new Ad
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            Title = request.Title.Trim(),
            ImageUrl = request.ImageUrl.Trim(),
            RedirectUrl = request.RedirectUrl.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        _context.Ads.Add(ad);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(userId, "AD_CREATED", "Ad", ad.Id,
            null, System.Text.Json.JsonSerializer.Serialize(ToAdDto(ad)),
            null, null, true);

        return ToAdDto(ad);
    }

    public async Task<AdDto?> UpdateAdAsync(Guid adId, UpdateAdRequest request, Guid userId)
    {
        var ad = await _context.Ads.FindAsync(adId);
        if (ad == null)
        {
            return null;
        }

        var oldSnapshot = ToAdDto(ad);

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            ad.Title = request.Title.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            ad.ImageUrl = request.ImageUrl.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.RedirectUrl))
        {
            ad.RedirectUrl = request.RedirectUrl.Trim();
        }

        if (request.Description != null)
        {
            ad.Description = request.Description.Trim();
        }

        await _context.SaveChangesAsync();

        await _auditService.LogAsync(userId, "AD_UPDATED", "Ad", ad.Id,
            System.Text.Json.JsonSerializer.Serialize(oldSnapshot),
            System.Text.Json.JsonSerializer.Serialize(ToAdDto(ad)),
            null, null, true);

        return ToAdDto(ad);
    }

    public async Task<bool> DeleteAdAsync(Guid adId, Guid userId)
    {
        var ad = await _context.Ads.FindAsync(adId);
        if (ad == null)
        {
            return false;
        }

        _context.Ads.Remove(ad);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(userId, "AD_DELETED", "Ad", adId,
            System.Text.Json.JsonSerializer.Serialize(ToAdDto(ad)),
            null,
            null, null, true);

        return true;
    }

    public async Task<TargetingRuleDto> CreateTargetingRuleAsync(Guid campaignId, CreateTargetingRuleRequest request, Guid userId)
    {
        var campaign = await _context.Campaigns.FindAsync(campaignId);
        if (campaign == null)
        {
            throw new KeyNotFoundException("Campaign not found");
        }

        var rule = new TargetingRule
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            RuleType = request.RuleType.Trim(),
            RuleValue = request.RuleValue.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.TargetingRules.Add(rule);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(userId, "TARGETING_RULE_CREATED", "TargetingRule", rule.Id,
            null, System.Text.Json.JsonSerializer.Serialize(ToTargetingRuleDto(rule)),
            null, null, true);

        return ToTargetingRuleDto(rule);
    }

    public async Task<TargetingRuleDto?> UpdateTargetingRuleAsync(Guid targetingRuleId, UpdateTargetingRuleRequest request, Guid userId)
    {
        var rule = await _context.TargetingRules.FindAsync(targetingRuleId);
        if (rule == null)
        {
            return null;
        }

        var oldSnapshot = ToTargetingRuleDto(rule);

        if (!string.IsNullOrWhiteSpace(request.RuleType))
        {
            rule.RuleType = request.RuleType.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.RuleValue))
        {
            rule.RuleValue = request.RuleValue.Trim();
        }

        await _context.SaveChangesAsync();

        await _auditService.LogAsync(userId, "TARGETING_RULE_UPDATED", "TargetingRule", rule.Id,
            System.Text.Json.JsonSerializer.Serialize(oldSnapshot),
            System.Text.Json.JsonSerializer.Serialize(ToTargetingRuleDto(rule)),
            null, null, true);

        return ToTargetingRuleDto(rule);
    }

    public async Task<bool> DeleteTargetingRuleAsync(Guid targetingRuleId, Guid userId)
    {
        var rule = await _context.TargetingRules.FindAsync(targetingRuleId);
        if (rule == null)
        {
            return false;
        }

        _context.TargetingRules.Remove(rule);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(userId, "TARGETING_RULE_DELETED", "TargetingRule", targetingRuleId,
            System.Text.Json.JsonSerializer.Serialize(ToTargetingRuleDto(rule)),
            null,
            null, null, true);

        return true;
    }

    private static CampaignDto ToCampaignDto(Campaign campaign)
    {
        return new CampaignDto
        {
            Id = campaign.Id,
            Name = campaign.Name,
            AdvertiserId = campaign.AdvertiserId,
            Status = campaign.Status,
            CpmBid = campaign.CpmBid,
            DailyBudget = campaign.DailyBudget,
            LifetimeBudget = campaign.LifetimeBudget,
            SpentToday = campaign.SpentToday,
            LifetimeSpent = campaign.LifetimeSpent,
            CreatedAt = campaign.CreatedAt,
            UpdatedAt = campaign.UpdatedAt,
            Ads = campaign.Ads?.Select(ToAdDto).ToList() ?? new List<AdDto>(),
            TargetingRules = campaign.TargetingRules?.Select(ToTargetingRuleDto).ToList() ?? new List<TargetingRuleDto>()
        };
    }

    private static AdDto ToAdDto(Ad ad)
    {
        return new AdDto
        {
            Id = ad.Id,
            CampaignId = ad.CampaignId,
            Title = ad.Title,
            ImageUrl = ad.ImageUrl,
            RedirectUrl = ad.RedirectUrl,
            Description = ad.Description,
            CreatedAt = ad.CreatedAt
        };
    }

    private static TargetingRuleDto ToTargetingRuleDto(TargetingRule rule)
    {
        return new TargetingRuleDto
        {
            Id = rule.Id,
            CampaignId = rule.CampaignId,
            RuleType = rule.RuleType,
            RuleValue = rule.RuleValue,
            CreatedAt = rule.CreatedAt
        };
    }
}
