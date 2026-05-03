using BidEngine.Shared;

namespace BidEngine.Services.Interfaces;

public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent @event) where TEvent : AdEventBase;
}
