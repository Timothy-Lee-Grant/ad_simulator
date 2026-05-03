using BidEngine.Shared;

namespace BidEngine.Services.Interfaces;

public interface IAdEventService
{
    Task PublishImpressionAsync(AdImpressionEvent impressionEvent);
    Task PublishClickAsync(AdClickEvent clickEvent);
    Task<CampaignMetricsDto> GetCampaignMetricsAsync(Guid campaignId, DateTime from, DateTime to);
}
