using BidEngine.Shared;

namespace BidEngine.Services.Interfaces;

public interface IExperimentContextAccessor
{
    ExperimentAssignment? CurrentAssignment { get; set; }
}
