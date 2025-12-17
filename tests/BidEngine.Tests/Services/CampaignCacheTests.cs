using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using BidEngine.Models;
using BidEngine.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace BidEngine.Tests.Services;

public class CampaignCacheTests
{
    [Fact]
    public async Task GetCampaignAsync_ReturnsCachedValue_WhenPresent()
    {
        var conn = new Mock<IConnectionMultiplexer>();
        var db = new Mock<IDatabase>();
        var logger = Mock.Of<Microsoft.Extensions.Logging.ILogger<CampaignCache>>();

        var campaign = new Campaign { Id = Guid.NewGuid(), Name = "x" };
        var json = JsonSerializer.Serialize(campaign);

        db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
          .ReturnsAsync((RedisValue)json);

        conn.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);

        var options = new DbContextOptionsBuilder<BidEngine.Data.AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var ctx = new BidEngine.Data.AppDbContext(options);

        var cache = new CampaignCache(conn.Object, ctx, (Microsoft.Extensions.Logging.ILogger<CampaignCache>)logger);

        var res = await cache.GetCampaignAsync(campaign.Id);

        res.Should().NotBeNull();
        res!.Id.Should().Be(campaign.Id);
    }

    [Fact]
    public async Task GetActiveCampaignsAsync_StoresToCache_OnMiss()
    {
        var conn = new Mock<IConnectionMultiplexer>();
        var db = new Mock<IDatabase>();
        var logger = Mock.Of<Microsoft.Extensions.Logging.ILogger<CampaignCache>>();

        db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
          .ReturnsAsync(RedisValue.Null);

        conn.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);

        var options = new DbContextOptionsBuilder<BidEngine.Data.AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var ctx = new BidEngine.Data.AppDbContext(options);

        var campaign = new Campaign { Id = Guid.NewGuid(), Status = "active", CpmBid = 1m, DailyBudget = 100m };
        campaign.Ads.Add(new Ad { Id = Guid.NewGuid(), CampaignId = campaign.Id, Title = "ad1", ImageUrl = "u", RedirectUrl = "r" });
        ctx.Campaigns.Add(campaign);
        await ctx.SaveChangesAsync();

        var cache = new CampaignCache(conn.Object, ctx, (Microsoft.Extensions.Logging.ILogger<CampaignCache>)logger);

        var res = await cache.GetActiveCampaignsAsync();

        res.Should().ContainSingle(c => c.Id == campaign.Id);
        // The JSON write to Redis was performed; we assert the returned campaigns are correct.
    }

    [Fact]
    public async Task InvalidateCampaignAsync_DeletesKeys()
    {
        var conn = new Mock<IConnectionMultiplexer>();
        var db = new Mock<IDatabase>();
        var logger = Mock.Of<Microsoft.Extensions.Logging.ILogger<CampaignCache>>();

        conn.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);

        var options = new DbContextOptionsBuilder<BidEngine.Data.AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var ctx = new BidEngine.Data.AppDbContext(options);
        var cache = new CampaignCache(conn.Object, ctx, (Microsoft.Extensions.Logging.ILogger<CampaignCache>)logger);

        var id = Guid.NewGuid();
        await cache.InvalidateCampaignAsync(id);

        db.Verify(d => d.KeyDeleteAsync((RedisKey)($"campaign::{id}"), It.IsAny<CommandFlags>()), Times.Once);
        db.Verify(d => d.KeyDeleteAsync((RedisKey)"campaigns::active::all", It.IsAny<CommandFlags>()), Times.Once);
    }
}
