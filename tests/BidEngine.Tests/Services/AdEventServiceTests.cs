using System;
using System.Threading.Tasks;
using BidEngine.Services;
using BidEngine.Services.Interfaces;
using BidEngine.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BidEngine.Tests.Services;

public class AdEventServiceTests
{
    [Fact]
    public async Task PublishImpressionAsync_PersistsAggregateAndEventLog()
    {
        var options = new DbContextOptionsBuilder<BidEngine.Data.AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var ctx = new BidEngine.Data.AppDbContext(options);
        var repository = new AdAnalyticsRepository(ctx);
        var publisher = new DbEventPublisher(ctx);
        var service = new AdEventService(publisher, repository);

        var impression = new AdImpressionEvent
        {
            CampaignId = Guid.NewGuid(),
            AdId = Guid.NewGuid(),
            UserId = "user-1",
            PlacementId = "homepage_banner",
            RequestId = Guid.NewGuid().ToString(),
            BidPrice = 3.60m,
            ImpressionValue = 0.0036m,
            TimestampUtc = DateTime.UtcNow,
            ExperimentId = "exp-1",
            VariationId = "var-a"
        };

        await service.PublishImpressionAsync(impression);

        var aggregate = await ctx.AdEventAggregates.FirstOrDefaultAsync();
        var log = await ctx.AdEventLogs.FirstOrDefaultAsync();

        aggregate.Should().NotBeNull();
        aggregate!.ImpressionCount.Should().Be(1);
        aggregate.SpendTotal.Should().Be(impression.ImpressionValue);
        aggregate.ExperimentId.Should().Be(impression.ExperimentId);
        aggregate.VariationId.Should().Be(impression.VariationId);

        log.Should().NotBeNull();
        log!.EventType.Should().Be("impression");
        log.CampaignId.Should().Be(impression.CampaignId);
    }

    [Fact]
    public async Task PublishClickAsync_PersistsClickCountAndEventLog()
    {
        var options = new DbContextOptionsBuilder<BidEngine.Data.AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var ctx = new BidEngine.Data.AppDbContext(options);
        var repository = new AdAnalyticsRepository(ctx);
        var publisher = new DbEventPublisher(ctx);
        var service = new AdEventService(publisher, repository);

        var click = new AdClickEvent
        {
            CampaignId = Guid.NewGuid(),
            AdId = Guid.NewGuid(),
            UserId = "user-2",
            PlacementId = "sidebar_300x250",
            RequestId = Guid.NewGuid().ToString(),
            ClickValue = 0m,
            TimestampUtc = DateTime.UtcNow,
            ExperimentId = "exp-2",
            VariationId = "var-b"
        };

        await service.PublishClickAsync(click);

        var aggregate = await ctx.AdEventAggregates.FirstOrDefaultAsync();
        var log = await ctx.AdEventLogs.FirstOrDefaultAsync();

        aggregate.Should().NotBeNull();
        aggregate!.ClickCount.Should().Be(1);
        aggregate.ImpressionCount.Should().Be(0);
        aggregate.ExperimentId.Should().Be(click.ExperimentId);
        aggregate.VariationId.Should().Be(click.VariationId);

        log.Should().NotBeNull();
        log!.EventType.Should().Be("click");
        log.CampaignId.Should().Be(click.CampaignId);
    }

    [Fact]
    public async Task GetCampaignMetricsAsync_ReturnsAggregatedMetrics()
    {
        var options = new DbContextOptionsBuilder<BidEngine.Data.AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var ctx = new BidEngine.Data.AppDbContext(options);
        var repository = new AdAnalyticsRepository(ctx);

        var eventDate = DateTime.UtcNow.Date;
        ctx.AdEventAggregates.Add(new AdEventAggregate
        {
            Id = Guid.NewGuid(),
            CampaignId = Guid.NewGuid(),
            AdId = Guid.NewGuid(),
            Date = eventDate,
            ImpressionCount = 5,
            ClickCount = 2,
            SpendTotal = 0.018m,
            ExperimentId = "exp-3",
            VariationId = "var-c"
        });
        await ctx.SaveChangesAsync();

        var metrics = await repository.GetCampaignMetricsAsync(ctx.AdEventAggregates.First().CampaignId, eventDate, eventDate);

        metrics.Impressions.Should().Be(5);
        metrics.Clicks.Should().Be(2);
        metrics.SpendTotal.Should().Be(0.018m);
        metrics.Ctr.Should().Be(0.4m);
        metrics.ExperimentBreakdown.Should().ContainSingle();
    }
}
