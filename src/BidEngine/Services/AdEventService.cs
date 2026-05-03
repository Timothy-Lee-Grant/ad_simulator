using BidEngine.Services.Interfaces;
using BidEngine.Shared;
using Prometheus;

namespace BidEngine.Services;

public class AdEventService : IAdEventService
{
    private static readonly Counter ImpressionCounter = Metrics.CreateCounter(
        "ad_impressions_total",
        "Total number of ad impressions recorded by the attribution pipeline");

    private static readonly Counter ClickCounter = Metrics.CreateCounter(
        "ad_clicks_total",
        "Total number of ad clicks recorded by the attribution pipeline");

    private readonly IEventPublisher _eventPublisher;
    private readonly IAdAnalyticsRepository _analyticsRepository;

    public AdEventService(IEventPublisher eventPublisher, IAdAnalyticsRepository analyticsRepository)
    {
        _eventPublisher = eventPublisher;
        _analyticsRepository = analyticsRepository;
    }

    public async Task PublishImpressionAsync(AdImpressionEvent impressionEvent)
    {
        ImpressionCounter.Inc();
        await _eventPublisher.PublishAsync(impressionEvent);
        await _analyticsRepository.AddImpressionAsync(impressionEvent);
    }

    public async Task PublishClickAsync(AdClickEvent clickEvent)
    {
        ClickCounter.Inc();
        await _eventPublisher.PublishAsync(clickEvent);
        await _analyticsRepository.AddClickAsync(clickEvent);
    }

    public Task<CampaignMetricsDto> GetCampaignMetricsAsync(Guid campaignId, DateTime from, DateTime to)
    {
        return _analyticsRepository.GetCampaignMetricsAsync(campaignId, from, to);
    }
}
