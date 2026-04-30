using BidEngine.Data;
using BidEngine.Shared;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace BidEngine.Services;

public class SemanticQueryService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SemanticQueryService> _logger;

    public SemanticQueryService(AppDbContext dbContext, ILogger<SemanticQueryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<Ad>> PerformSemanticSearchForTop3Ads(Vector targetVector)
    {
        _logger.LogInformation("Running semantic search for top ads");

        var topAds = await _dbContext.Ads
            .FromSqlInterpolated($@"
                SELECT * FROM ads
                ORDER BY embedding <=> {targetVector}
                LIMIT 3")
            .ToListAsync();

        return topAds;
    }
}
