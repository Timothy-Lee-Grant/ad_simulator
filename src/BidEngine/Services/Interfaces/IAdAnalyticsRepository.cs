using BidEngine.Shared;

namespace BidEngine.Services.Interfaces;

public interface IAdAnalyticsRepository
{
    Task AddImpressionAsync(AdImpressionEvent impressionEvent);
    Task AddClickAsync(AdClickEvent clickEvent);
    Task<CampaignMetricsDto> GetCampaignMetricsAsync(Guid campaignId, DateTime from, DateTime to);
}
