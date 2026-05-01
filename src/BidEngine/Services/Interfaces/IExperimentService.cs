using BidEngine.Shared;

namespace BidEngine.Services.Interfaces;

public interface IExperimentService
{
    Task<ExperimentAssignment?> AssignVariationAsync(BidEngine.Shared.BidRequest request);
    Task<ExperimentDefinition?> GetExperimentAsync(string experimentId);
    Task<IEnumerable<ExperimentDefinition>> GetActiveExperimentsAsync();
}
