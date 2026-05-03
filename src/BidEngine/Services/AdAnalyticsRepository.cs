using BidEngine.Data;
using BidEngine.Services.Interfaces;
using BidEngine.Shared;
using Microsoft.EntityFrameworkCore;

namespace BidEngine.Services;

public class AdAnalyticsRepository : IAdAnalyticsRepository
{
    private readonly AppDbContext _dbContext;

    public AdAnalyticsRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddImpressionAsync(AdImpressionEvent impressionEvent)
    {
        var eventDate = impressionEvent.TimestampUtc.Date;
        var aggregate = await _dbContext.AdEventAggregates
            .FirstOrDefaultAsync(a => a.CampaignId == impressionEvent.CampaignId
                && a.AdId == impressionEvent.AdId
                && a.Date == eventDate
                && a.ExperimentId == impressionEvent.ExperimentId
                && a.VariationId == impressionEvent.VariationId);

        if (aggregate == null)
        {
            aggregate = new AdEventAggregate
            {
                Id = Guid.NewGuid(),
                CampaignId = impressionEvent.CampaignId,
                AdId = impressionEvent.AdId,
                Date = eventDate,
                ExperimentId = impressionEvent.ExperimentId,
                VariationId = impressionEvent.VariationId,
                ImpressionCount = 0,
                ClickCount = 0,
                SpendTotal = 0m
            };
            _dbContext.AdEventAggregates.Add(aggregate);
        }

        aggregate.ImpressionCount += 1;
        aggregate.SpendTotal += impressionEvent.ImpressionValue;

        await _dbContext.SaveChangesAsync();
    }

    public async Task AddClickAsync(AdClickEvent clickEvent)
    {
        var eventDate = clickEvent.TimestampUtc.Date;
        var aggregate = await _dbContext.AdEventAggregates
            .FirstOrDefaultAsync(a => a.CampaignId == clickEvent.CampaignId
                && a.AdId == clickEvent.AdId
                && a.Date == eventDate
                && a.ExperimentId == clickEvent.ExperimentId
                && a.VariationId == clickEvent.VariationId);

        if (aggregate == null)
        {
            aggregate = new AdEventAggregate
            {
                Id = Guid.NewGuid(),
                CampaignId = clickEvent.CampaignId,
                AdId = clickEvent.AdId,
                Date = eventDate,
                ExperimentId = clickEvent.ExperimentId,
                VariationId = clickEvent.VariationId,
                ImpressionCount = 0,
                ClickCount = 0,
                SpendTotal = 0m
            };
            _dbContext.AdEventAggregates.Add(aggregate);
        }

        aggregate.ClickCount += 1;
        await _dbContext.SaveChangesAsync();
    }

    public async Task<CampaignMetricsDto> GetCampaignMetricsAsync(Guid campaignId, DateTime from, DateTime to)
    {
        var fromDate = from.Date;
        var toDate = to.Date;

        var query = _dbContext.AdEventAggregates
            .Where(a => a.CampaignId == campaignId && a.Date >= fromDate && a.Date <= toDate);

        var totals = await query
            .GroupBy(a => 1)
            .Select(g => new
            {
                Impressions = g.Sum(x => x.ImpressionCount),
                Clicks = g.Sum(x => x.ClickCount),
                SpendTotal = g.Sum(x => x.SpendTotal)
            })
            .FirstOrDefaultAsync();

        var breakdown = await query
            .GroupBy(a => new { a.ExperimentId, a.VariationId })
            .Select(g => new CampaignExperimentMetricsDto
            {
                ExperimentId = g.Key.ExperimentId,
                VariationId = g.Key.VariationId,
                Impressions = g.Sum(x => x.ImpressionCount),
                Clicks = g.Sum(x => x.ClickCount),
                SpendTotal = g.Sum(x => x.SpendTotal),
                Ctr = g.Sum(x => x.ImpressionCount) > 0 ? (decimal)g.Sum(x => x.ClickCount) / g.Sum(x => x.ImpressionCount) : 0m
            })
            .ToListAsync();

        var impressionCount = totals?.Impressions ?? 0;
        var clickCount = totals?.Clicks ?? 0;
        var spendTotal = totals?.SpendTotal ?? 0m;

        return new CampaignMetricsDto
        {
            CampaignId = campaignId,
            Impressions = impressionCount,
            Clicks = clickCount,
            SpendTotal = spendTotal,
            Ctr = impressionCount > 0 ? (decimal)clickCount / impressionCount : 0m,
            Revenue = 0m,
            FromDate = fromDate,
            ToDate = toDate,
            ExperimentBreakdown = breakdown
        };
    }
}
