using BidEngine.Shared;

namespace BidEngine.Services.Interfaces;

public interface IExperimentAssignmentService
{
    ExperimentAssignment AssignVariation(ExperimentDefinition experiment, string bucketKey);
}
