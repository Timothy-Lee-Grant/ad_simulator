using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace BidEngine.Tests.Integration;

// NOTE: These integration tests are intentionally skipped by default.
// To run them locally, ensure you have a Redis instance available and
// set the REDIS_URL environment variable (e.g., redis://localhost:6379).
public class CampaignCacheIntegrationTests
{
    [Fact(Skip = "Integration test - requires Redis and real DB")]
    public async Task GetActiveCampaignsAsync_CachesResults_InRedis()
    {
        // skeleton: start a real redis, create sqlite db with campaigns,
        // call GetActiveCampaignsAsync twice and assert that second call
        // reads from redis (no DB hit) and returns identical serialized list.
        await Task.CompletedTask;
        Assert.True(true);
    }
}
