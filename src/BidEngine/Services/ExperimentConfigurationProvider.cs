using BidEngine.Services.Interfaces;
using BidEngine.Shared;
using Microsoft.Extensions.Options;

namespace BidEngine.Services;

public class ExperimentConfigurationProvider : IExperimentConfigurationProvider
{
    private readonly ExperimentOptions _options;

    public ExperimentConfigurationProvider(IOptions<ExperimentOptions> options)
    {
        _options = options.Value;
    }

    public IEnumerable<ExperimentDefinition> GetExperiments()
    {
        return _options.Experiments ?? Enumerable.Empty<ExperimentDefinition>();
    }

    public ExperimentDefinition? GetExperiment(string experimentId)
    {
        return GetExperiments()
            .FirstOrDefault(x => string.Equals(x.Id, experimentId, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<ExperimentDefinition> GetActiveExperiments()
    {
        var now = DateTime.UtcNow;
        return GetExperiments()
            .Where(x => x.Enabled)
            .Where(x => x.StartTime == null || x.StartTime <= now)
            .Where(x => x.EndTime == null || x.EndTime >= now);
    }
}
