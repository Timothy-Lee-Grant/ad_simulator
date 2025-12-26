using BidEngine.Data;
using BidEngine.Shared;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using AllMiniLmL6V2Sharp;
using Microsoft.AspNetCore.Mvc.ViewFeatures;



namespace BidEngine.Services;

public class CampaignCache
{
    //Tim Grant I am considering what architecture in the C# code I should use. 
    //Because I am going to be connecting to the same postgres database And to the same Redis database for both my semantics search and my normal highest bidding auction. 
    //I think it makes sense for me to put all of the logic for the database connections into the same class.
    //Instead of having two separate functions that both have connections to the exact same database.
    //But this might mean that I should rename my class to be something more generic, because right now the name does not necessarily indicate what it's actually doing.  
    //Gemini suggests naming the class AdEngineDataService
    private readonly IDatabase _redis;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<CampaignCache> _logger;
    private readonly bool _allowDeterministicFallback;
    private const int CacheTtlSeconds = 300; // 5 minutes

    public CampaignCache(IConnectionMultiplexer db, AppDbContext context, ILogger<CampaignCache> logger, Microsoft.Extensions.Options.IOptions<EmbeddingOptions> opts)
    {
        _redis = db.GetDatabase();
        _dbContext = context;
        _logger = logger;
        _allowDeterministicFallback = opts?.Value?.AllowDeterministicFallback ?? false;
    }

    public async Task<Campaign?> GetCampaignAsync(Guid campaignId)
    {

        var cacheKey = $"campaign::{campaignId}";
        
        // Step 1: Try to get from Redis cache
        var cached = await _redis.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            _logger.LogInformation("Cache hit for campaign {CampaignId}", campaignId);
            var campaign = JsonSerializer.Deserialize<Campaign>(cached.ToString());
            return campaign;
        }

        //if no cashe hit. need to find in sql, get campaign, store it in redis, return found object
        _logger.LogInformation("Cache miss for campaign {CampaignId}, querying database", campaignId);
        //this entire thing simplifies to one sql statement. It will look like
        /*
        SELECT c.Id, c.Name, a.Id, a.CampaignId, a.Title
        FROM Campaigns c
        LEFT JOIN Ads a ON c.Id = a.CampaignId
        WHERE c.Id = 1;

        */
        var dbCampaign = await _dbContext.Campaigns
            .Include(c => c.Ads)
            .Include(c => c.TargetingRules)
            .FirstOrDefaultAsync(c => c.Id == campaignId);

        if(dbCampaign != null)
        {
            // Serialize with options to avoid circular reference errors
            var options = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };

            var json = JsonSerializer.Serialize(dbCampaign, options);
            await _redis.StringSetAsync(cacheKey, json, TimeSpan.FromSeconds(CacheTtlSeconds));
        }
        
        return dbCampaign;
    }

    /// <summary>
    /// Get all active campaigns, checking cache first
    /// </summary>
    public async Task<List<Campaign>> GetActiveCampaignsAsync()
    {
        var casheKey = "campaigns::active::all";

        var cashed = await _redis.
            StringGetAsync(casheKey);

        if (cashed.HasValue)
        {
            _logger.LogInformation("Cache hit for active campaigns");
            var res1 = JsonSerializer.Deserialize<List<Campaign>>(cashed.ToString());
            return res1 ?? new();
        }

        _logger.LogInformation("Cache miss for active campaigns, querying database");
        var res = await _dbContext.Campaigns //I initially had '_dbContext.Campaigns', but it caused an error, look into learning more about how plural is mapped with C# Tim Grant
            .Include(c=>c.Ads)
            .Include(c=>c.TargetingRules)
            .Where(c => c.Status == "active")
            .ToListAsync();

        // Use options to avoid circular reference serialization errors (Ads -> Campaign -> Ads)
        var options = new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };

        var json = JsonSerializer.Serialize(res, options);
        await _redis.StringSetAsync(casheKey, json, TimeSpan.FromSeconds(CacheTtlSeconds));
        return res;
        //I guess my way of doing this is not accurate....
        /*
        var cashedActiveCampaigns = await _redis.SomeMethod(campaign => campaign.status == "active");
        var sqlActiveCampaigns = await _dbContext.Campaign
            .find(c => c.status == "active");

        return cashedActiveCampaigns + sqlActiveCampaigns;
        */
    }

    public async Task InvalidateCampaignAsync (Guid campaignId)
    {
        var casheKey = $"campaign::{campaignId}";
        await _redis.KeyDeleteAsync(casheKey);
        await _redis.KeyDeleteAsync("campaigns::active::all");
        _logger.LogInformation("Invalidated cache for campaign {CampaignId}", campaignId);
    }

    public async Task<Pgvector.Vector?> FindVectorFromVideoId(Guid videoId)
    {
        //var result = await _dbContext.Videos.Include(something=>something.Embedding).Where(s=>s.VideoId).GetAsync();
        //var result = await _dbContext.Videos.Where(passedInParameter.Id == videoId).SingleOrDefaultAsync();
        
        var video = await _dbContext.Videos
        .AsNoTracking() // Performance boost: we aren't changing the data, just reading
        .Where(v => v.Id == videoId)
        .Select(v => new { v.Embedding }) // Optional: Only pull the vector from SQL, not the whole row
        .FirstOrDefaultAsync();

        return video?.Embedding;
    }

    public async Task CreateVectorFromVideoId(Guid videoId)
    {

        var video = await _dbContext.Videos.FindAsync(videoId);

        if(video is null || string.IsNullOrWhiteSpace(video.Description))
        {
            return;
        }
        
        // Try to use the real model if present; otherwise fall back to a deterministic
        // embedding so local development and seeding works without the model files.
        try
        {
            using var embedder = new AllMiniLmL6V2Embedder();
            var embedding = embedder.GenerateEmbedding(video.Description).ToArray();
            video.Embedding = new Pgvector.Vector(embedding);
        }
        catch (System.IO.IOException ex)
        {
            // If fallback is allowed for development, use deterministic embedding. Otherwise make the failure explicit.
            if (_allowDeterministicFallback)
            {
                _logger.LogWarning(ex, "Model files not found, using fallback deterministic embedding for video {VideoId}", videoId);
                var embedding = ComputeDeterministicEmbedding(video.Description);
                video.Embedding = new Pgvector.Vector(embedding);
            }
            else
            {
                _logger.LogError(ex, "Embedder unavailable and deterministic fallback is disabled. Aborting embedding generation for video {VideoId}", videoId);
                throw new InvalidOperationException("Native embedder not available and deterministic fallback is disabled. Provide model files in './model' or set Embeddings:AllowDeterministicFallback=true.", ex);
            }
        }

        await _dbContext.SaveChangesAsync();


    }

    public async Task GenerateEmbeddingsForAllVideos()
    {
        // 1. Attempt to load the native model once; if it's not available, we'll
        // use a fallback deterministic embedding generator so seeding still works.
        AllMiniLmL6V2Embedder? embedder = null;
        bool haveNativeEmbedder = false;
        try
        {
            embedder = new AllMiniLmL6V2Embedder();
            haveNativeEmbedder = true;
        }
        catch (System.IO.IOException ex)
        {
            if (_allowDeterministicFallback)
            {
                _logger.LogWarning(ex, "AllMiniLm model not found; falling back to deterministic embeddings. Place model files in './model' to use the native embedder.");
                haveNativeEmbedder = false;
            }
            else
            {
                _logger.LogError(ex, "AllMiniLm model not found and deterministic fallback is disabled. Aborting seed run.");
                throw new InvalidOperationException("Native embedder not available and deterministic fallback is disabled. Provide model files in './model' or set Embeddings:AllowDeterministicFallback=true.", ex);
            }
        }

        // 2. Stream the videos that don't have embeddings yet
        var videoStream = _dbContext.Videos
            .Where(v => v.Embedding == null)
            .AsAsyncEnumerable(); 

        await foreach (var video in videoStream)
        {
            if (string.IsNullOrWhiteSpace(video.Description))
            {
                _logger.LogInformation("Skipping video {VideoId} because it has no description", video.Id);
                continue;
            }

            float[] vectorArray;
            if (haveNativeEmbedder && embedder != null)
            {
                vectorArray = embedder.GenerateEmbedding(video.Description).ToArray();
            }
            else
            {
                vectorArray = ComputeDeterministicEmbedding(video.Description);
            }

            video.Embedding = new Pgvector.Vector(vectorArray);
            _logger.LogInformation("Generated embedding for: {Title}", video.Title);
        }

        // 3. Save all changes at once at the end (or in chunks)
        await _dbContext.SaveChangesAsync();

        if (embedder != null)
        {
            embedder.Dispose();
        }
    }

    /// <summary>
    /// Inserts small sample data for manual testing if the tables are empty.
    /// This helps validate end-to-end seeding inside the container.
    /// </summary>
    public async Task SeedSampleDataAsync()
    {
        // Seed one sample video if none exist
        if (!await _dbContext.Videos.AnyAsync())
        {
            _dbContext.Videos.Add(new Video
            {
                Title = "Sample Video for Seeding",
                Description = "Highlights and walkthrough of the 2025 gaming tournament, featuring strategy and clips.",
            });
        }

        // Seed one sample ad if none exist
        if (!await _dbContext.Ads.AnyAsync())
        {
            _dbContext.Ads.Add(new Ad
            {
                Title = "Sample Ad - Gaming Gear",
                Description = "High-performance gaming controller with low-latency haptics.",
                CampaignId = Guid.NewGuid(),
            });
        }

        await _dbContext.SaveChangesAsync();
    }

    private static float[] ComputeDeterministicEmbedding(string text, int dims = 384)
    {
        var res = new float[dims];
        using var sha = System.Security.Cryptography.SHA256.Create();
        for (int i = 0; i < dims; i++)
        {
            var input = System.Text.Encoding.UTF8.GetBytes(text + "|" + i);
            var hash = sha.ComputeHash(input);
            var val = BitConverter.ToInt32(hash, 0);
            // Normalize to roughly -1..1
            res[i] = val / (float)int.MaxValue;
        }
        return res;
    }

    //Tim Grant - I just realized that I don't really have any actual endpoints for creating and adding data from my C# into the database. 
    //If I did this, I would need to give the user a frontend to be able to actually perform those operations. This is something I have not built out yet but might be valuable in the future. 
    
}
