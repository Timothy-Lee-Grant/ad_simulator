using System.Text.Json;
using BidEngine.Data;
using BidEngine.Services.Interfaces;
using BidEngine.Shared;

namespace BidEngine.Services;

public class DbEventPublisher : IEventPublisher
{
    private readonly AppDbContext _dbContext;

    public DbEventPublisher(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : AdEventBase
    {
        var log = new AdEventLog
        {
            Id = Guid.NewGuid(),
            EventType = @event.EventType,
            TimestampUtc = @event.TimestampUtc,
            CampaignId = @event.CampaignId,
            AdId = @event.AdId,
            UserId = @event.UserId,
            PlacementId = @event.PlacementId,
            RequestId = @event.RequestId,
            ExperimentId = @event.ExperimentId,
            VariationId = @event.VariationId,
            PayloadJson = JsonSerializer.Serialize(@event)
        };

        _dbContext.AdEventLogs.Add(log);
        await _dbContext.SaveChangesAsync();
    }
}
