using System;
using System.Threading.Tasks;
using BidEngine.Controllers;
using BidEngine.Shared;
using Moq;
using BidEngine.Services;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BidEngine.Tests.Controllers;

public class BidControllerTests
{
    [Fact]
    public async Task EvaluateBidsAsync_ReturnsBadRequest_WhenMissingUserIdOrPlacementId()
    {
        var conn = new Mock<StackExchange.Redis.IConnectionMultiplexer>();
        conn.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(Mock.Of<StackExchange.Redis.IDatabase>());
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<BidEngine.Data.AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var ctx = new BidEngine.Data.AppDbContext(options);
        var strategy = new Mock<IBiddingStrategy>();
        strategy.Setup(s => s.SelectWinningBidAsync(It.IsAny<BidRequest>())).ReturnsAsync((BidResponse?)null);
        var selector = new BidEngine.Services.BidSelector(strategy.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger<BidEngine.Services.BidSelector>>());
        var budget = new BudgetService(ctx, conn.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger<BudgetService>>(), new CampaignCache(conn.Object, ctx, Mock.Of<Microsoft.Extensions.Logging.ILogger<CampaignCache>>()));
        var controller = new BidController(selector, budget, Mock.Of<Microsoft.Extensions.Logging.ILogger<BidController>>());

        var bad = await controller.EvaluateBidsAsync(new BidRequest { UserId = "", PlacementId = "" });
        bad.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task EvaluateBidsAsync_ReturnsNoContent_WhenNoWinningBid()
    {
        var conn = new Mock<StackExchange.Redis.IConnectionMultiplexer>();
        conn.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(Mock.Of<StackExchange.Redis.IDatabase>());
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<BidEngine.Data.AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var ctx = new BidEngine.Data.AppDbContext(options);
        var strategy = new Mock<IBiddingStrategy>();
        strategy.Setup(s => s.SelectWinningBidAsync(It.IsAny<BidRequest>())).ReturnsAsync((BidResponse?)null);
        var selector = new BidEngine.Services.BidSelector(strategy.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger<BidEngine.Services.BidSelector>>());
        var budget = new BudgetService(ctx, conn.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger<BudgetService>>(), new CampaignCache(conn.Object, ctx, Mock.Of<Microsoft.Extensions.Logging.ILogger<CampaignCache>>()));
        var controller = new BidController(selector, budget, Mock.Of<Microsoft.Extensions.Logging.ILogger<BidController>>());

        var res = await controller.EvaluateBidsAsync(new BidRequest { UserId = "u", PlacementId = "p" });
        res.Result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task EvaluateBidsAsync_ReturnsServiceUnavailable_WhenBudgetDeductionFails()
    {
        var conn = new Mock<StackExchange.Redis.IConnectionMultiplexer>();
        var db = new Mock<StackExchange.Redis.IDatabase>();
        conn.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<BidEngine.Data.AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var ctx = new BidEngine.Data.AppDbContext(options);
        // create a campaign that is eligible but will fail budget deduction due to small remaining budget
        var campaign = new Campaign { Id = Guid.NewGuid(), Status = "active", CpmBid = 2m, DailyBudget = 1m, SpentToday = 0.999m };
        campaign.Ads.Add(new Ad { Id = Guid.NewGuid(), CampaignId = campaign.Id, Title = "ad", ImageUrl = "i", RedirectUrl = "r" });
        ctx.Campaigns.Add(campaign);
        await ctx.SaveChangesAsync();

        var strategy = new Mock<IBiddingStrategy>();
        strategy.Setup(s => s.SelectWinningBidAsync(It.IsAny<BidRequest>()))
            .ReturnsAsync(new BidResponse
            {
                CampaignId = campaign.Id,
                AdId = campaign.Ads[0].Id,
                BidPrice = campaign.CpmBid,
                AdContent = new AdContent { Title = "ad", ImageUrl = "i", RedirectUrl = "r", Description = "" },
                Confidence = 0.95
            });
        var selector = new BidEngine.Services.BidSelector(strategy.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger<BidEngine.Services.BidSelector>>());
        var budget = new BudgetService(ctx, conn.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger<BudgetService>>(), new CampaignCache(conn.Object, ctx, Mock.Of<Microsoft.Extensions.Logging.ILogger<CampaignCache>>()));

        var controller = new BidController(selector, budget, Mock.Of<Microsoft.Extensions.Logging.ILogger<BidController>>());
        var res = await controller.EvaluateBidsAsync(new BidRequest { UserId = "u", PlacementId = "p" });

        res.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task EvaluateBidsAsync_ReturnsOkAndCallsBudgetService_WhenSuccess()
    {
        var conn = new Mock<StackExchange.Redis.IConnectionMultiplexer>();
        var db = new Mock<StackExchange.Redis.IDatabase>();
        conn.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<BidEngine.Data.AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var ctx = new BidEngine.Data.AppDbContext(options);
        var campaign = new Campaign { Id = Guid.NewGuid(), Status = "active", CpmBid = 2m, DailyBudget = 100m, SpentToday = 0m };
        campaign.Ads.Add(new Ad { Id = Guid.NewGuid(), CampaignId = campaign.Id, Title = "ad", ImageUrl = "i", RedirectUrl = "r" });
        ctx.Campaigns.Add(campaign);
        await ctx.SaveChangesAsync();

        var strategy = new Mock<IBiddingStrategy>();
        strategy.Setup(s => s.SelectWinningBidAsync(It.IsAny<BidRequest>()))
            .ReturnsAsync(new BidResponse
            {
                CampaignId = campaign.Id,
                AdId = campaign.Ads[0].Id,
                BidPrice = campaign.CpmBid,
                AdContent = new AdContent { Title = "ad", ImageUrl = "i", RedirectUrl = "r", Description = "" },
                Confidence = 0.95
            });
        var selector = new BidEngine.Services.BidSelector(strategy.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger<BidEngine.Services.BidSelector>>());
        var budget = new BudgetService(ctx, conn.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger<BudgetService>>(), new CampaignCache(conn.Object, ctx, Mock.Of<Microsoft.Extensions.Logging.ILogger<CampaignCache>>()));

        var controller = new BidController(selector, budget, Mock.Of<Microsoft.Extensions.Logging.ILogger<BidController>>());
        var res = await controller.EvaluateBidsAsync(new BidRequest { UserId = "u", PlacementId = "p" });

        res.Result.Should().BeOfType<OkObjectResult>();
    }
}
