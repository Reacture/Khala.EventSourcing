namespace Khala.FakeDomain.Events
{
    using System;
    using Khala.EventSourcing;

    public abstract class FakeDomainEvent : DomainEvent
    {
        protected static readonly Random _random = new Random();

        protected FakeDomainEvent()
        {
            SourceId = Guid.NewGuid();
            Version = _random.Next();
            RaisedAt = DateTimeOffset.Now.AddTicks(_random.Next());
        }
    }
}
