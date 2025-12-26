namespace BidEngine.Services
{
    public class EmbeddingOptions
    {
        /// <summary>
        /// When true, allow falling back to a deterministic (sha256-based) embedding
        /// when native model files are not available. Default: false.
        /// </summary>
        public bool AllowDeterministicFallback { get; set; } = false;
    }
}
