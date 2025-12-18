namespace BidEngine.Services;

public interface IExperimentService
{
    /// <summary>
    /// Returns the assigned variant key for the given experiment and identity.
    /// Should be deterministic for the same identity to ensure sticky assignments.
    /// Example return values: "A", "B", "control", "treatment"
    /// </summary>
    string GetVariant(string experimentName, string identity);
}
