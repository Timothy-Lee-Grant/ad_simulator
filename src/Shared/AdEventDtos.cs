using System.Text.Json.Serialization;

namespace BidEngine.Shared;

public abstract class AdEventBase
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public Guid CampaignId { get; set; }
    public Guid AdId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string PlacementId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string? ExperimentId { get; set; }
    public string? VariationId { get; set; }
    public string? AttributionId { get; set; }
    public string? Source { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class AdImpressionEvent : AdEventBase
{
    public decimal BidPrice { get; set; }
    public decimal ImpressionValue { get; set; }
    public string? AdContentType { get; set; }

    public AdImpressionEvent()
    {
        EventType = "impression";
    }
}

public class AdClickEvent : AdEventBase
{
    public decimal ClickValue { get; set; }
    public string? ClickLocation { get; set; }
    public string? SessionId { get; set; }

    public AdClickEvent()
    {
        EventType = "click";
        Source = "click";
    }
}

public class CampaignMetricsDto
{
    public Guid CampaignId { get; set; }
    public long Impressions { get; set; }
    public long Clicks { get; set; }
    public decimal SpendTotal { get; set; }
    public decimal Ctr { get; set; }
    public decimal Revenue { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public IReadOnlyList<CampaignExperimentMetricsDto> ExperimentBreakdown { get; set; } = Array.Empty<CampaignExperimentMetricsDto>();
}

public class CampaignExperimentMetricsDto
{
    public string? ExperimentId { get; set; }
    public string? VariationId { get; set; }
    public long Impressions { get; set; }
    public long Clicks { get; set; }
    public decimal SpendTotal { get; set; }
    public decimal Ctr { get; set; }
}

public class AdEventLog
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public Guid CampaignId { get; set; }
    public Guid AdId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string PlacementId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string? ExperimentId { get; set; }
    public string? VariationId { get; set; }
    public string? PayloadJson { get; set; }
}

public class AdEventAggregate
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid AdId { get; set; }
    public DateTime Date { get; set; }
    public long ImpressionCount { get; set; }
    public long ClickCount { get; set; }
    public decimal SpendTotal { get; set; }
    public string? ExperimentId { get; set; }
    public string? VariationId { get; set; }
}
