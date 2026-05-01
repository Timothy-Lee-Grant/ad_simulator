using BidEngine.Shared;

namespace BidEngine.Services.Interfaces;

public interface IExperimentEventLogger
{
    void LogExposure(ExperimentAssignment? assignment, BidRequest request, string result);
    void LogOutcome(ExperimentAssignment? assignment, BidRequest request, string metric, decimal value = 1);
}
