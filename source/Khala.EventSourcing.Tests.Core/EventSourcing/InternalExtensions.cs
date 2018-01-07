namespace Khala.EventSourcing
{
    using System;
    using System.Collections.Generic;

    internal static class InternalExtensions
    {
        public static void Raise(
            this IReadOnlyList<DomainEvent> events,
            Guid sourceId,
            int versionOffset = default)
        {
            for (int i = 0; i < events.Count; i++)
            {
                events[i].SourceId = sourceId;
                events[i].Version = versionOffset + i + 1;
                events[i].RaisedAt = DateTimeOffset.Now;
            }
        }

        public static void Raise(
            this DomainEvent domainEvent,
            Guid sourceId,
            int versionOffset = default)
        {
            domainEvent.SourceId = sourceId;
            domainEvent.Version = versionOffset + 1;
            domainEvent.RaisedAt = DateTimeOffset.Now;
        }
    }
}
