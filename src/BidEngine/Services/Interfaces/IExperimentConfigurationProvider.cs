using BidEngine.Shared;

namespace BidEngine.Services.Interfaces;

public interface IExperimentConfigurationProvider
{
    IEnumerable<ExperimentDefinition> GetExperiments();
    ExperimentDefinition? GetExperiment(string experimentId);
    IEnumerable<ExperimentDefinition> GetActiveExperiments();
}
