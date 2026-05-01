namespace BidEngine.Shared;

public class ExperimentOptions
{
    public List<ExperimentDefinition> Experiments { get; set; } = new();
}

public class ExperimentDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Enabled { get; set; } = true;
    public int TrafficAllocation { get; set; } = 100;
    public string Seed { get; set; } = string.Empty;
    public List<ExperimentVariation> Variations { get; set; } = new();
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

public class ExperimentVariation
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Strategy { get; set; } = string.Empty;
    public int Weight { get; set; } = 0;
    public string? Description { get; set; }
}

public class ExperimentAssignment
{
    public string ExperimentId { get; set; } = string.Empty;
    public string ExperimentName { get; set; } = string.Empty;
    public string VariationId { get; set; } = string.Empty;
    public string VariationName { get; set; } = string.Empty;
    public string StrategyName { get; set; } = string.Empty;
    public bool IsAssigned { get; set; }
    public bool IsControl { get; set; }
    public int Bucket { get; set; }
    public string BucketKey { get; set; } = string.Empty;
    public int TrafficAllocation { get; set; }
}

public class ExperimentExposureRequest
{
    public string ExperimentId { get; set; } = string.Empty;
    public string VariationId { get; set; } = string.Empty;
    public string VariationName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public Guid? CampaignId { get; set; }
    public string Result { get; set; } = string.Empty;
}

public class ExperimentOutcomeRequest
{
    public string ExperimentId { get; set; } = string.Empty;
    public string VariationId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public Guid? CampaignId { get; set; }
    public string Metric { get; set; } = string.Empty;
    public decimal Value { get; set; }
}
