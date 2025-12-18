using System.Text;
using System.Security.Cryptography;

namespace BidEngine.Services;

/// <summary>
/// Lightweight deterministic experiment assignment using hashing.
/// It does not require external storage and is suitable for simple A/B tests.
/// Allocations are expressed as percentages for variants; by default A/B split is 50/50.
/// </summary>
public class HashExperimentService : IExperimentService
{
    private readonly Dictionary<string, (string VariantA, int WeightA, string VariantB, int WeightB)> _experiments;

    public HashExperimentService()
    {
        // Default experiments can be wired from configuration later.
        _experiments = new Dictionary<string, (string, int, string, int)>()
        {
            { "bid-selector", ("A", 50, "B", 50) }
        };
    }

    public string GetVariant(string experimentName, string identity)
    {
        if (string.IsNullOrEmpty(identity)) identity = "anonymous";

        if (!_experiments.TryGetValue(experimentName, out var cfg))
        {
            // default behavior: single control group
            return "control";
        }

        var bucket = ComputeBucket(experimentName + ":" + identity);
        var threshold = cfg.WeightA;
        return bucket < threshold ? cfg.VariantA : cfg.VariantB;
    }

    private int ComputeBucket(string input)
    {
        // deterministic 0-99 bucket via SHA256
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        // use first 4 bytes as uint
        var v = BitConverter.ToUInt32(hash, 0);
        return (int)(v % 100);
    }
}
