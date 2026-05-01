using BidEngine.Services.Interfaces;
using BidEngine.Shared;
using System.Security.Cryptography;
using System.Text;

namespace BidEngine.Services;

public class ExperimentAssignmentService : IExperimentAssignmentService
{
    public ExperimentAssignment AssignVariation(ExperimentDefinition experiment, string bucketKey)
    {
        var assignment = new ExperimentAssignment
        {
            ExperimentId = experiment.Id,
            ExperimentName = experiment.Name,
            BucketKey = bucketKey,
            TrafficAllocation = Math.Clamp(experiment.TrafficAllocation, 0, 100)
        };

        if (!experiment.Enabled)
        {
            assignment.IsAssigned = false;
            assignment.VariationId = "control";
            assignment.VariationName = "Control";
            assignment.StrategyName = string.Empty;
            assignment.IsControl = true;
            return assignment;
        }

        var hashValue = ComputeStableHash(experiment.Id, bucketKey, experiment.Seed);
        assignment.Bucket = hashValue % 100;

        if (assignment.Bucket >= assignment.TrafficAllocation)
        {
            assignment.IsAssigned = false;
            assignment.VariationId = "control";
            assignment.VariationName = "Control";
            assignment.StrategyName = string.Empty;
            assignment.IsControl = true;
            return assignment;
        }

        var variation = SelectVariation(experiment.Variations, assignment.Bucket);
        assignment.IsAssigned = variation != null;
        assignment.VariationId = variation?.Id ?? "control";
        assignment.VariationName = variation?.Name ?? "Control";
        assignment.StrategyName = variation?.Strategy ?? string.Empty;
        assignment.IsControl = string.Equals(assignment.VariationId, "control", StringComparison.OrdinalIgnoreCase);

        return assignment;
    }

    private static int ComputeStableHash(string experimentId, string bucketKey, string seed)
    {
        using var sha256 = SHA256.Create();
        var input = string.Join("|", experimentId, bucketKey, seed ?? string.Empty);
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        var value = BitConverter.ToUInt32(hash, 0);
        return (int)(value % 100);
    }

    private static ExperimentVariation? SelectVariation(IEnumerable<ExperimentVariation> variations, int bucket)
    {
        var normalizedVariations = variations
            .Where(v => v.Weight > 0)
            .ToList();

        if (!normalizedVariations.Any())
        {
            return null;
        }

        var totalWeight = normalizedVariations.Sum(v => v.Weight);
        if (totalWeight <= 0)
        {
            return null;
        }

        var scaledBucket = (bucket * totalWeight) / 100;
        var cumulative = 0;

        foreach (var variation in normalizedVariations)
        {
            cumulative += variation.Weight;
            if (scaledBucket < cumulative)
            {
                return variation;
            }
        }

        return normalizedVariations.Last();
    }
}
