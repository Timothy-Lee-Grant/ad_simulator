using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BidEngine.Models;
using BidEngine.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BidEngine.Tests.Services;

public class BidSelectorTests
{
    [Fact]
    public async Task SelectWinningBidAsync_ReturnsNull_WhenNoActiveCampaigns()
    {
        var conn = new Mock<StackExchange.Redis.IConnectionMultiplexer>();
        var db = new Mock<StackExchange.Redis.IDatabase>();
        conn.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);

        var options = new DbContextOptionsBuilder<BidEngine.Data.AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var ctx = new BidEngine.Data.AppDbContext(options);
        var cache = new CampaignCache(conn.Object, ctx, Mock.Of<Microsoft.Extensions.Logging.ILogger<CampaignCache>>());
        var selector = new BidSelector(cache, Mock.Of<Microsoft.Extensions.Logging.ILogger<BidSelector>>());

        var res = await selector.SelectWinningBidAsync(new BidRequest { UserId = "u", PlacementId = "p" });
        res.Should().BeNull();
    }

    [Fact]
    public async Task SelectWinningBidAsync_FiltersOutCampaigns_ThatCannotServe()
    {
        var c1 = new Campaign { Id = Guid.NewGuid(), Status = "active", CpmBid = 1m, DailyBudget = 100m, SpentToday = 100m }; // can't serve
        var c2 = new Campaign { Id = Guid.NewGuid(), Status = "active", CpmBid = 2m, DailyBudget = 100m, SpentToday = 0m };
        c2.Ads.Add(new Ad { Id = Guid.NewGuid(), CampaignId = c2.Id, Title = "ad2", ImageUrl = "u", RedirectUrl = "r" });

        var conn = new Mock<StackExchange.Redis.IConnectionMultiplexer>();
        var db = new Mock<StackExchange.Redis.IDatabase>();
        conn.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);
        var options = new DbContextOptionsBuilder<BidEngine.Data.AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var ctx = new BidEngine.Data.AppDbContext(options);
        ctx.Campaigns.AddRange(c1, c2);
        await ctx.SaveChangesAsync();

        var cache = new CampaignCache(conn.Object, ctx, Mock.Of<Microsoft.Extensions.Logging.ILogger<CampaignCache>>());
        var selector = new BidSelector(cache, Mock.Of<Microsoft.Extensions.Logging.ILogger<BidSelector>>());

        var res = await selector.SelectWinningBidAsync(new BidRequest { UserId = "u", PlacementId = "p" });
        res.Should().NotBeNull();
        res!.CampaignId.Should().Be(c2.Id);
    }

    [Fact]
    public async Task SelectWinningBidAsync_SelectsHighestCpm_WhenMultipleEligible()
    {
        var c1 = new Campaign { Id = Guid.NewGuid(), Status = "active", CpmBid = 1m, DailyBudget = 100m, SpentToday = 0m };
        c1.Ads.Add(new Ad { Id = Guid.NewGuid(), CampaignId = c1.Id, Title = "ad1", ImageUrl = "u", RedirectUrl = "r" });
        var c2 = new Campaign { Id = Guid.NewGuid(), Status = "active", CpmBid = 5m, DailyBudget = 100m, SpentToday = 0m };
        c2.Ads.Add(new Ad { Id = Guid.NewGuid(), CampaignId = c2.Id, Title = "ad2", ImageUrl = "u", RedirectUrl = "r" });

        var conn = new Mock<StackExchange.Redis.IConnectionMultiplexer>();
        var db = new Mock<StackExchange.Redis.IDatabase>();
        conn.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);
        var options = new DbContextOptionsBuilder<BidEngine.Data.AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var ctx = new BidEngine.Data.AppDbContext(options);
        ctx.Campaigns.AddRange(c1, c2);
        await ctx.SaveChangesAsync();

        var cache = new CampaignCache(conn.Object, ctx, Mock.Of<Microsoft.Extensions.Logging.ILogger<CampaignCache>>());
        var selector = new BidSelector(cache, Mock.Of<Microsoft.Extensions.Logging.ILogger<BidSelector>>());

        var res = await selector.SelectWinningBidAsync(new BidRequest { UserId = "u", PlacementId = "p" });
        res.Should().NotBeNull();
        res!.CampaignId.Should().Be(c2.Id);
        res.BidPrice.Should().Be(c2.CpmBid);
    }

    [Fact]
    public async Task MatchesTargetingRules_IsCaseInsensitive()
    {
        var campaign = new Campaign { Id = Guid.NewGuid(), Status = "active", CpmBid = 1m, DailyBudget = 100m, SpentToday = 0m };
        campaign.TargetingRules.Add(new TargetingRule { Id = Guid.NewGuid(), RuleType = "country", RuleValue = "us" });
        campaign.Ads.Add(new Ad { Id = Guid.NewGuid(), CampaignId = campaign.Id, Title = "ad", ImageUrl = "i", RedirectUrl = "r" });

        var conn = new Mock<StackExchange.Redis.IConnectionMultiplexer>();
        var db = new Mock<StackExchange.Redis.IDatabase>();
        conn.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);
        var options = new DbContextOptionsBuilder<BidEngine.Data.AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var ctx = new BidEngine.Data.AppDbContext(options);
        ctx.Campaigns.Add(campaign);
        await ctx.SaveChangesAsync();

        var cache = new CampaignCache(conn.Object, ctx, Mock.Of<Microsoft.Extensions.Logging.ILogger<CampaignCache>>());
        var selector = new BidSelector(cache, Mock.Of<Microsoft.Extensions.Logging.ILogger<BidSelector>>());

        var res = await selector.SelectWinningBidAsync(new BidRequest { UserId = "u", PlacementId = "p", CountryCode = "US" });
        res.Should().NotBeNull();
    }
}
