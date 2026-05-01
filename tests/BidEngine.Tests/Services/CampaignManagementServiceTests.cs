using BidEngine.Data;
using BidEngine.Services;
using BidEngine.Services.Interfaces;
using BidEngine.Shared;
using BidEngine.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BidEngine.Tests.Services;

public class CampaignManagementServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<IAuditService> _auditServiceMock;
    private readonly CampaignManagementService _service;

    public CampaignManagementServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _auditServiceMock = new Mock<IAuditService>();
        _service = new CampaignManagementService(_context, _auditServiceMock.Object);
    }

    [Fact]
    public async Task CreateCampaignAsync_ValidRequest_CreatesCampaignAndLogsAudit()
    {
        var request = new CreateCampaignRequest
        {
            Name = "Phase 3 Campaign",
            AdvertiserId = Guid.NewGuid(),
            Status = "active",
            CpmBid = 1.25m,
            DailyBudget = 50m,
            LifetimeBudget = 500m,
            Ads = new List<CreateAdRequest>
            {
                new CreateAdRequest
                {
                    Title = "Test Ad",
                    ImageUrl = "https://example.com/ad.jpg",
                    RedirectUrl = "https://example.com",
                    Description = "Test ad description"
                }
            },
            TargetingRules = new List<CreateTargetingRuleRequest>
            {
                new CreateTargetingRuleRequest { RuleType = "location", RuleValue = "US" }
            }
        };

        var result = await _service.CreateCampaignAsync(request, Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Equal(request.Name, result.Name);
        Assert.Single(result.Ads);
        Assert.Single(result.TargetingRules);
        Assert.Equal(request.AdvertiserId, result.AdvertiserId);

        _auditServiceMock.Verify(x => x.LogAsync(
            It.IsAny<Guid>(),
            "CAMPAIGN_CREATED",
            "Campaign",
            result.Id,
            null,
            It.Is<string>(s => s.Contains(request.Name)),
            null,
            null,
            true,
            null), Times.Once);
    }

    [Fact]
    public async Task UpdateCampaignAsync_ChangesFields_UpdatesCampaignAndLogsAudit()
    {
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Name = "Original Campaign",
            AdvertiserId = Guid.NewGuid(),
            Status = "active",
            CpmBid = 0.75m,
            DailyBudget = 20m,
            LifetimeBudget = 200m,
            SpentToday = 0m,
            LifetimeSpent = 0m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Campaigns.Add(campaign);
        await _context.SaveChangesAsync();

        var request = new UpdateCampaignRequest
        {
            Name = "Updated Campaign",
            Status = "paused",
            CpmBid = 0.95m,
            DailyBudget = 30m
        };

        var updated = await _service.UpdateCampaignAsync(campaign.Id, request, Guid.NewGuid());

        Assert.NotNull(updated);
        Assert.Equal(request.Name, updated!.Name);
        Assert.Equal(request.Status, updated.Status);
        Assert.Equal(request.CpmBid, updated.CpmBid);
        Assert.Equal(request.DailyBudget, updated.DailyBudget);

        _auditServiceMock.Verify(x => x.LogAsync(
            It.IsAny<Guid>(),
            "CAMPAIGN_UPDATED",
            "Campaign",
            campaign.Id,
            It.Is<string>(s => s.Contains("Original Campaign")),
            It.Is<string>(s => s.Contains("Updated Campaign")),
            null,
            null,
            true,
            null), Times.Once);
    }

    [Fact]
    public async Task DeleteCampaignAsync_ExistingCampaign_RemovesCampaignAndLogsAudit()
    {
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Name = "Delete Campaign",
            AdvertiserId = Guid.NewGuid(),
            Status = "active",
            CpmBid = 1.00m,
            DailyBudget = 10m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Campaigns.Add(campaign);
        await _context.SaveChangesAsync();

        var deleted = await _service.DeleteCampaignAsync(campaign.Id, Guid.NewGuid());

        Assert.True(deleted);
        Assert.Null(await _context.Campaigns.FindAsync(campaign.Id));

        _auditServiceMock.Verify(x => x.LogAsync(
            It.IsAny<Guid>(),
            "CAMPAIGN_DELETED",
            "Campaign",
            campaign.Id,
            It.Is<string>(s => s.Contains(campaign.Name)),
            null,
            null,
            null,
            true,
            null), Times.Once);
    }

    [Fact]
    public async Task CreateAdAsync_ValidRequest_AddsAdAndLogsAudit()
    {
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Name = "Ad Campaign",
            AdvertiserId = Guid.NewGuid(),
            Status = "active",
            CpmBid = 1.00m,
            DailyBudget = 100m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Campaigns.Add(campaign);
        await _context.SaveChangesAsync();

        var request = new CreateAdRequest
        {
            Title = "New Ad",
            ImageUrl = "https://example.com/image.jpg",
            RedirectUrl = "https://example.com/landing",
            Description = "New ad description"
        };

        var ad = await _service.CreateAdAsync(campaign.Id, request, Guid.NewGuid());

        Assert.NotNull(ad);
        Assert.Equal(campaign.Id, ad.CampaignId);
        Assert.Equal(request.Title, ad.Title);

        _auditServiceMock.Verify(x => x.LogAsync(
            It.IsAny<Guid>(),
            "AD_CREATED",
            "Ad",
            ad.Id,
            null,
            It.Is<string>(s => s.Contains(request.Title)),
            null,
            null,
            true,
            null), Times.Once);
    }

    [Fact]
    public async Task UpdateAdAsync_ValidRequest_UpdatesAdAndLogsAudit()
    {
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Name = "Update Ad Campaign",
            AdvertiserId = Guid.NewGuid(),
            Status = "active",
            CpmBid = 1.00m,
            DailyBudget = 100m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var ad = new Ad
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            Title = "Original Ad",
            ImageUrl = "https://example.com/original.jpg",
            RedirectUrl = "https://example.com/original",
            Description = "Original description",
            CreatedAt = DateTime.UtcNow
        };

        _context.Campaigns.Add(campaign);
        _context.Ads.Add(ad);
        await _context.SaveChangesAsync();

        var request = new UpdateAdRequest
        {
            Title = "Updated Ad",
            ImageUrl = "https://example.com/updated.jpg",
            RedirectUrl = "https://example.com/updated",
            Description = "Updated description"
        };

        var updatedAd = await _service.UpdateAdAsync(ad.Id, request, Guid.NewGuid());

        Assert.NotNull(updatedAd);
        Assert.Equal(request.Title, updatedAd!.Title);
        Assert.Equal(request.ImageUrl, updatedAd.ImageUrl);

        _auditServiceMock.Verify(x => x.LogAsync(
            It.IsAny<Guid>(),
            "AD_UPDATED",
            "Ad",
            ad.Id,
            It.Is<string>(s => s.Contains("Original Ad")),
            It.Is<string>(s => s.Contains("Updated Ad")),
            null,
            null,
            true,
            null), Times.Once);
    }

    [Fact]
    public async Task DeleteAdAsync_ExistingAd_RemovesAdAndLogsAudit()
    {
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Name = "Delete Ad Campaign",
            AdvertiserId = Guid.NewGuid(),
            Status = "active",
            CpmBid = 1.00m,
            DailyBudget = 100m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var ad = new Ad
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            Title = "Removable Ad",
            ImageUrl = "https://example.com/remove.jpg",
            RedirectUrl = "https://example.com/remove",
            Description = "Delete me",
            CreatedAt = DateTime.UtcNow
        };

        _context.Campaigns.Add(campaign);
        _context.Ads.Add(ad);
        await _context.SaveChangesAsync();

        var deleted = await _service.DeleteAdAsync(ad.Id, Guid.NewGuid());

        Assert.True(deleted);
        Assert.Null(await _context.Ads.FindAsync(ad.Id));

        _auditServiceMock.Verify(x => x.LogAsync(
            It.IsAny<Guid>(),
            "AD_DELETED",
            "Ad",
            ad.Id,
            It.Is<string>(s => s.Contains(ad.Title)),
            null,
            null,
            null,
            true,
            null), Times.Once);
    }

    [Fact]
    public async Task CreateTargetingRuleAsync_ValidRequest_AddsTargetingRuleAndLogsAudit()
    {
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Name = "Targeting Campaign",
            AdvertiserId = Guid.NewGuid(),
            Status = "active",
            CpmBid = 1.00m,
            DailyBudget = 100m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Campaigns.Add(campaign);
        await _context.SaveChangesAsync();

        var request = new CreateTargetingRuleRequest
        {
            RuleType = "gender",
            RuleValue = "female"
        };

        var rule = await _service.CreateTargetingRuleAsync(campaign.Id, request, Guid.NewGuid());

        Assert.NotNull(rule);
        Assert.Equal(campaign.Id, rule.CampaignId);
        Assert.Equal(request.RuleType, rule.RuleType);

        _auditServiceMock.Verify(x => x.LogAsync(
            It.IsAny<Guid>(),
            "TARGETING_RULE_CREATED",
            "TargetingRule",
            rule.Id,
            null,
            It.Is<string>(s => s.Contains(request.RuleType)),
            null,
            null,
            true,
            null), Times.Once);
    }

    [Fact]
    public async Task UpdateTargetingRuleAsync_ValidRequest_UpdatesRuleAndLogsAudit()
    {
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Name = "Update Targeting Campaign",
            AdvertiserId = Guid.NewGuid(),
            Status = "active",
            CpmBid = 1.00m,
            DailyBudget = 100m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var rule = new TargetingRule
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            RuleType = "location",
            RuleValue = "US",
            CreatedAt = DateTime.UtcNow
        };

        _context.Campaigns.Add(campaign);
        _context.TargetingRules.Add(rule);
        await _context.SaveChangesAsync();

        var request = new UpdateTargetingRuleRequest
        {
            RuleType = "browser",
            RuleValue = "Chrome"
        };

        var updated = await _service.UpdateTargetingRuleAsync(rule.Id, request, Guid.NewGuid());

        Assert.NotNull(updated);
        Assert.Equal(request.RuleType, updated!.RuleType);
        Assert.Equal(request.RuleValue, updated.RuleValue);

        _auditServiceMock.Verify(x => x.LogAsync(
            It.IsAny<Guid>(),
            "TARGETING_RULE_UPDATED",
            "TargetingRule",
            rule.Id,
            It.Is<string>(s => s.Contains("location")),
            It.Is<string>(s => s.Contains("browser")),
            null,
            null,
            true,
            null), Times.Once);
    }

    [Fact]
    public async Task DeleteTargetingRuleAsync_ExistingRule_RemovesRuleAndLogsAudit()
    {
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Name = "Delete Targeting Campaign",
            AdvertiserId = Guid.NewGuid(),
            Status = "active",
            CpmBid = 1.00m,
            DailyBudget = 100m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var rule = new TargetingRule
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            RuleType = "interests",
            RuleValue = "sports",
            CreatedAt = DateTime.UtcNow
        };

        _context.Campaigns.Add(campaign);
        _context.TargetingRules.Add(rule);
        await _context.SaveChangesAsync();

        var deleted = await _service.DeleteTargetingRuleAsync(rule.Id, Guid.NewGuid());

        Assert.True(deleted);
        Assert.Null(await _context.TargetingRules.FindAsync(rule.Id));

        _auditServiceMock.Verify(x => x.LogAsync(
            It.IsAny<Guid>(),
            "TARGETING_RULE_DELETED",
            "TargetingRule",
            rule.Id,
            It.Is<string>(s => s.Contains(rule.RuleType)),
            null,
            null,
            null,
            true,
            null), Times.Once);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
