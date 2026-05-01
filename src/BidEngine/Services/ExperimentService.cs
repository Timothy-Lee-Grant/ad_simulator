using BidEngine.Services.Interfaces;
using BidEngine.Shared;

namespace BidEngine.Services;

public class ExperimentService : IExperimentService
{
    private readonly IExperimentConfigurationProvider _configurationProvider;
    private readonly IExperimentAssignmentService _assignmentService;

    public ExperimentService(
        IExperimentConfigurationProvider configurationProvider,
        IExperimentAssignmentService assignmentService)
    {
        _configurationProvider = configurationProvider;
        _assignmentService = assignmentService;
    }

    public async Task<ExperimentAssignment?> AssignVariationAsync(BidRequest request)
    {
        var experiments = _configurationProvider.GetActiveExperiments().ToList();
        if (!experiments.Any())
        {
            return null;
        }

        // For the first implementation, use the first active experiment.
        var experiment = experiments.First();
        var bucketKey = BuildBucketKey(request, experiment);
        var assignment = _assignmentService.AssignVariation(experiment, bucketKey);

        return await Task.FromResult(assignment);
    }

    public async Task<ExperimentDefinition?> GetExperimentAsync(string experimentId)
    {
        return await Task.FromResult(_configurationProvider.GetExperiment(experimentId));
    }

    public async Task<IEnumerable<ExperimentDefinition>> GetActiveExperimentsAsync()
    {
        return await Task.FromResult(_configurationProvider.GetActiveExperiments());
    }

    private static string BuildBucketKey(BidRequest request, ExperimentDefinition experiment)
    {
        var userId = string.IsNullOrWhiteSpace(request.UserId) ? "anonymous" : request.UserId.Trim();
        return string.Join("|", experiment.Id, userId, request.PlacementId ?? string.Empty, request.CountryCode ?? string.Empty);
    }
}
