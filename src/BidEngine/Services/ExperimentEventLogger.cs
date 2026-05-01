using BidEngine.Services.Interfaces;
using BidEngine.Shared;
using Prometheus;

namespace BidEngine.Services;

public class ExperimentEventLogger : IExperimentEventLogger
{
    private static readonly Counter ExperimentExposures = Metrics.CreateCounter(
        "experiment_exposures_total",
        "Number of experiment exposures",
        new CounterConfiguration { LabelNames = new[] { "experiment_id", "variation_id", "result" } });

    private static readonly Counter ExperimentOutcomes = Metrics.CreateCounter(
        "experiment_outcomes_total",
        "Number of experiment outcome events",
        new CounterConfiguration { LabelNames = new[] { "experiment_id", "variation_id", "metric" } });

    public void LogExposure(ExperimentAssignment? assignment, BidRequest request, string result)
    {
        if (assignment == null)
        {
            return;
        }

        ExperimentExposures.WithLabels(
            assignment.ExperimentId,
            assignment.VariationId,
            result)
            .Inc();
    }

    public void LogOutcome(ExperimentAssignment? assignment, BidRequest request, string metric, decimal value = 1)
    {
        if (assignment == null)
        {
            return;
        }

        ExperimentOutcomes.WithLabels(
            assignment.ExperimentId,
            assignment.VariationId,
            metric)
            .Inc((double)value);
    }
}
