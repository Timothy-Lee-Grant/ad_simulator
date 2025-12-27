using BidEngine.Data;
using BidEngine.Shared;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using AllMiniLmL6V2Sharp;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using AllMiniLmL6V2Sharp.Tokenizer;



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
    private const int CacheTtlSeconds = 300; // 5 minutes

    public CampaignCache(IConnectionMultiplexer db, AppDbContext context, ILogger<CampaignCache> logger)
    {
        _redis = db.GetDatabase();
        _dbContext = context;
        _logger = logger;
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
        
        //Tim Grant - One of the suggestions which Gemini just told me to do was not to have this embedder object created each time I run this function, because this will cause my application to crash in the future if I do this many times. So it suggested the singleton pattern. I will need to implement this in the future. 
        var tokenizer = new BertTokenizer("model/vocab.txt");
        using var embedder = new AllMiniLmL6V2Embedder("model/model.onnx", tokenizer);
        float[]? embedding = embedder.GenerateEmbedding(video.Description).ToArray();

        video.Embedding = new Pgvector.Vector(embedding);

        await _dbContext.SaveChangesAsync();


    }

    public async Task GenerateEmbeddingsForAllVideos()
    {
        var tokenizer = new BertTokenizer("model/vocab.txt");
        // 1. Load the embedder ONCE (Singleton-style)
        using var embedder = new AllMiniLmL6V2Embedder("model/model.onnx", tokenizer);

        // 2. Stream the videos that don't have embeddings yet
        var videoStream = _dbContext.Videos
            .Where(v => v.Embedding == null || v.Embedding.ToArray()[0] == 0.15f)
            .AsAsyncEnumerable(); 

        await foreach (var video in videoStream)
        {
            if (!string.IsNullOrWhiteSpace(video.Description))
            {
                // Generate the vector
                var vectorArray = embedder.GenerateEmbedding(video.Description).ToArray();
                video.Embedding = new Pgvector.Vector(vectorArray);
                
                Console.WriteLine($"Generated embedding for: {video.Title}");
            }
        }

        // 3. Save all changes at once at the end (or in chunks)
        await _dbContext.SaveChangesAsync();
    }


    public async Task GenerateEmbeddingsForAllVideosWithDebugging()
{
    _logger.LogInformation("Starting bulk vectorization...");

    var tokenizer = new BertTokenizer("model/vocab.txt");
    using var embedder = new AllMiniLmL6V2Embedder("model/model.onnx", tokenizer);

    // FIX: Pull all videos into memory first so we don't confuse the SQL translator
    // We use .ToListAsync() to execute the SQL immediately and get the objects into C#
    var allVideos = await _dbContext.Videos.ToListAsync();
    
    _logger.LogInformation("Checking {Count} videos for missing or default embeddings.", allVideos.Count);

    int processedCount = 0;

    foreach (var video in allVideos)
    {
        // Check if it's null OR contains our 'default' 0.15 value
        bool needsUpdate = video.Embedding == null || 
                          (video.Embedding.ToArray().Length > 0 && video.Embedding.ToArray()[0] == 0.15f);

        if (needsUpdate && !string.IsNullOrWhiteSpace(video.Description))
        {
            try 
            {
                var vectorArray = embedder.GenerateEmbedding(video.Description).ToArray();
                video.Embedding = new Pgvector.Vector(vectorArray);
                
                // Explicitly tell EF Core this specific video was changed
                _dbContext.Entry(video).State = EntityState.Modified;
                
                processedCount++;
                _logger.LogInformation("Generated real vector for: {Title}", video.Title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error embedding video: {Title}", video.Title);
            }
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

    //Tim Grant - I just realized that I don't really have any actual endpoints for creating and adding data from my C# into the database. 
    //If I did this, I would need to give the user a frontend to be able to actually perform those operations. This is something I have not built out yet but might be valuable in the future. 
    
}
