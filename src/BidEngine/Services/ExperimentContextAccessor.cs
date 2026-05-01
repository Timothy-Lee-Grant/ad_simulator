using BidEngine.Services.Interfaces;
using BidEngine.Shared;

namespace BidEngine.Services;

public class ExperimentContextAccessor : IExperimentContextAccessor
{
    public ExperimentAssignment? CurrentAssignment { get; set; }
}
