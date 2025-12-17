using System;
using System.Threading.Tasks;
using BidEngine.Models;
using BidEngine.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BidEngine.Tests.Services;

public class BudgetServiceTests
{
    [Fact]
    public async Task DeductBudgetAsync_ReturnsFalse_WhenCampaignNotFound()
    {
        var options = new DbContextOptionsBuilder<BidEngine.Data.AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var ctx = new BidEngine.Data.AppDbContext(options);
        var conn = new Mock<StackExchange.Redis.IConnectionMultiplexer>();
        var db = new Mock<StackExchange.Redis.IDatabase>();
        conn.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);
        var cache = new CampaignCache(conn.Object, ctx, Mock.Of<Microsoft.Extensions.Logging.ILogger<CampaignCache>>());
        var svc = new BudgetService(ctx, conn.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger<BudgetService>>(), cache);

        var res = await svc.DeductBudgetAsync(Guid.NewGuid(), 10m);
        res.Should().BeFalse();
    }

    [Fact]
    public async Task DeductBudgetAsync_SucceedsAndPersists_WhenBudgetAvailable()
    {
        var options = new DbContextOptionsBuilder<BidEngine.Data.AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var ctx = new BidEngine.Data.AppDbContext(options);
        var campaign = new Campaign { Id = Guid.NewGuid(), DailyBudget = 100m, SpentToday = 0m, LifetimeBudget = 1000m, LifetimeSpent = 0m };
        ctx.Campaigns.Add(campaign);
        await ctx.SaveChangesAsync();

        var conn2 = new Mock<StackExchange.Redis.IConnectionMultiplexer>();
        var db2 = new Mock<StackExchange.Redis.IDatabase>();
        conn2.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db2.Object);

        var cache2 = new CampaignCache(conn2.Object, ctx, Mock.Of<Microsoft.Extensions.Logging.ILogger<CampaignCache>>());
        var svc2 = new BudgetService(ctx, conn2.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger<BudgetService>>(), cache2);

        var res = await svc2.DeductBudgetAsync(campaign.Id, 2000m / 1000m); // cpm/n -> cost per impression
        res.Should().BeTrue();

        var updated = await ctx.Campaigns.FindAsync(campaign.Id);
        updated!.SpentToday.Should().BeGreaterThan(0);

        // verify redis keys deleted as part of cache invalidation
        db2.Verify(d => d.KeyDeleteAsync((StackExchange.Redis.RedisKey)($"campaign::{campaign.Id}"), It.IsAny<StackExchange.Redis.CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ResetDailyBudgetAsync_SetsSpentToZero_ForAllCampaigns()
    {
        var options = new DbContextOptionsBuilder<BidEngine.Data.AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var ctx = new BidEngine.Data.AppDbContext(options);
        ctx.Campaigns.Add(new Campaign { Id = Guid.NewGuid(), SpentToday = 10m });
        await ctx.SaveChangesAsync();

        var conn = new Mock<StackExchange.Redis.IConnectionMultiplexer>();
        conn.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(Mock.Of<StackExchange.Redis.IDatabase>());
        var cache = new Mock<CampaignCache>(MockBehavior.Strict, conn.Object, ctx, Mock.Of<Microsoft.Extensions.Logging.ILogger<CampaignCache>>());
        var svc = new BudgetService(ctx, conn.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger<BudgetService>>(), cache.Object);

        await svc.ResetDailyBudgetAsync();

        var c = await ctx.Campaigns.FirstAsync();
        c.SpentToday.Should().Be(0m);
    }
}
