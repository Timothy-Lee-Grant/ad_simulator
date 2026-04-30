using AllMiniLmL6V2Sharp;
using AllMiniLmL6V2Sharp.Tokenizer;
using BidEngine.Data;
using BidEngine.Shared;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace BidEngine.Services;

public class VideoEmbeddingService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<VideoEmbeddingService> _logger;

    public VideoEmbeddingService(AppDbContext dbContext, ILogger<VideoEmbeddingService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Vector?> FindVectorFromVideoId(Guid videoId)
    {
        var video = await _dbContext.Videos
            .AsNoTracking()
            .Where(v => v.Id == videoId)
            .Select(v => new { v.Embedding })
            .FirstOrDefaultAsync();

        return video?.Embedding;
    }

    public async Task CreateVectorFromVideoId(Guid videoId)
    {
        var video = await _dbContext.Videos.FindAsync(videoId);
        if (video == null || string.IsNullOrWhiteSpace(video.Description))
        {
            return;
        }

        var tokenizer = new BertTokenizer("model/vocab.txt");
        using var embedder = new AllMiniLmL6V2Embedder("model/model.onnx", tokenizer);
        var embedding = embedder.GenerateEmbedding(video.Description).ToArray();
        video.Embedding = new Vector(embedding);

        await _dbContext.SaveChangesAsync();
    }

    public async Task GenerateEmbeddingsForAllVideos()
    {
        var tokenizer = new BertTokenizer("model/vocab.txt");
        using var embedder = new AllMiniLmL6V2Embedder("model/model.onnx", tokenizer);

        var videoStream = _dbContext.Videos
            .Where(v => v.Embedding == null || v.Embedding.ToArray()[0] == 0.15f)
            .AsAsyncEnumerable();

        await foreach (var video in videoStream)
        {
            if (string.IsNullOrWhiteSpace(video.Description))
            {
                continue;
            }

            var vectorArray = embedder.GenerateEmbedding(video.Description).ToArray();
            video.Embedding = new Vector(vectorArray);
            _logger.LogInformation("Generated embedding for: {Title}", video.Title);
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task GenerateEmbeddingsForAllVideosWithDebugging()
    {
        _logger.LogInformation("Starting bulk vectorization...");

        var tokenizer = new BertTokenizer("model/vocab.txt");
        using var embedder = new AllMiniLmL6V2Embedder("model/model.onnx", tokenizer);

        var allVideos = await _dbContext.Videos.ToListAsync();
        _logger.LogInformation("Checking {Count} videos for missing or default embeddings.", allVideos.Count);

        int processedCount = 0;
        foreach (var video in allVideos)
        {
            bool needsUpdate = video.Embedding == null ||
                (video.Embedding.ToArray().Length > 0 && video.Embedding.ToArray()[0] == 0.15f);

            if (!needsUpdate || string.IsNullOrWhiteSpace(video.Description))
            {
                continue;
            }

            try
            {
                var vectorArray = embedder.GenerateEmbedding(video.Description).ToArray();
                video.Embedding = new Vector(vectorArray);
                _dbContext.Entry(video).State = EntityState.Modified;
                processedCount++;
                _logger.LogInformation("Generated real vector for: {Title}", video.Title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error embedding video: {Title}", video.Title);
            }

            if (video.CreatedAt.Kind == DateTimeKind.Unspecified)
            {
                video.CreatedAt = DateTime.SpecifyKind(video.CreatedAt, DateTimeKind.Utc);
            }
        }

        if (processedCount > 0)
        {
            var savedRows = await _dbContext.SaveChangesAsync();
            _logger.LogInformation("SUCCESS: Database updated. Videos vectorized: {Processed}, Rows affected: {Saved}", processedCount, savedRows);
        }
        else
        {
            _logger.LogInformation("No videos required updating.");
        }
    }

    public async Task GenerateEmbeddingsForAllAds()
    {
        var allAds = await _dbContext.Ads.ToListAsync();
        var tokenizer = new BertTokenizer("model/vocab.txt");
        using var embedder = new AllMiniLmL6V2Embedder("model/model.onnx", tokenizer);

        int processedCount = 0;
        _logger.LogInformation("Checking {Count} ads for missing or default embeddings.", allAds.Count);

        foreach (var singleAd in allAds)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(singleAd.Description))
                {
                    continue;
                }

                var singleEmbedding = embedder.GenerateEmbedding(singleAd.Description).ToArray();
                singleAd.Embedding = new Vector(singleEmbedding);
                if (singleAd.CreatedAt.Kind == DateTimeKind.Unspecified)
                {
                    singleAd.CreatedAt = DateTime.SpecifyKind(singleAd.CreatedAt, DateTimeKind.Utc);
                }

                _dbContext.Entry(singleAd).State = EntityState.Modified;
                _logger.LogInformation("Generated real vector for: {Title}", singleAd.Title);
                processedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error embedding ad: {Title}", singleAd.Title);
            }
        }

        if (processedCount > 0)
        {
            var savedRows = await _dbContext.SaveChangesAsync();
            _logger.LogInformation("SUCCESS: Database updated. Ads vectorized: {Processed}, Rows affected: {Saved}", processedCount, savedRows);
        }
        else
        {
            _logger.LogInformation("No ads required updating.");
        }
    }
}
