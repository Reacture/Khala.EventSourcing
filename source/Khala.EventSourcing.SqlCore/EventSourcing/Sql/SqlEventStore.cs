namespace Khala.EventSourcing.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Messaging;

#if NETSTANDARD2_0
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Storage;
#else
    using System.Data.Entity;
    using System.Data.Entity.Core;
    using System.Data.Entity.Infrastructure;
#endif

    public class SqlEventStore : ISqlEventStore
    {
        private readonly Func<EventStoreDbContext> _dbContextFactory;
        private readonly IMessageSerializer _serializer;

        public SqlEventStore(
            Func<EventStoreDbContext> dbContextFactory,
            IMessageSerializer serializer)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public Task SaveEvents<T>(
            IEnumerable<IDomainEvent> events,
            Guid? correlationId,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            List<IDomainEvent> domainEvents = events.ToList();

            if (domainEvents.Count == 0)
            {
                return Task.FromResult(true);
            }

            IDomainEvent firstEvent = domainEvents.First();

            for (int i = 0; i < domainEvents.Count; i++)
            {
                if (domainEvents[i] == null)
                {
                    throw new ArgumentException(
                        $"{nameof(events)} cannot contain null.",
                        nameof(events));
                }

                if (domainEvents[i].Version != firstEvent.Version + i)
                {
                    throw new ArgumentException(
                        $"Versions of {nameof(events)} must be sequential.",
                        nameof(events));
                }

                if (domainEvents[i].SourceId != firstEvent.SourceId)
                {
                    throw new ArgumentException(
                        $"All events must have the same source id.",
                        nameof(events));
                }
            }

            return Save<T>(firstEvent.SourceId, domainEvents, correlationId, null, cancellationToken);
        }

        public Task SaveEvents<T>(
            IEnumerable<IDomainEvent> events,
            Guid? correlationId,
            string contributor,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            List<IDomainEvent> domainEvents = events.ToList();

            if (domainEvents.Count == 0)
            {
                return Task.FromResult(true);
            }

            IDomainEvent firstEvent = domainEvents.First();

            for (int i = 0; i < domainEvents.Count; i++)
            {
                if (domainEvents[i] == null)
                {
                    throw new ArgumentException(
                        $"{nameof(events)} cannot contain null.",
                        nameof(events));
                }

                if (domainEvents[i].Version != firstEvent.Version + i)
                {
                    throw new ArgumentException(
                        $"Versions of {nameof(events)} must be sequential.",
                        nameof(events));
                }

                if (domainEvents[i].SourceId != firstEvent.SourceId)
                {
                    throw new ArgumentException(
                        $"All events must have the same source id.",
                        nameof(events));
                }
            }

            return Save<T>(firstEvent.SourceId, domainEvents, correlationId, contributor, cancellationToken);
        }

        private async Task Save<T>(
            Guid sourceId,
            List<IDomainEvent> events,
            Guid? correlationId,
            string contributor,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            using (EventStoreDbContext context = _dbContextFactory.Invoke())
            {
                await UpsertAggregate<T>(context, sourceId, events, cancellationToken).ConfigureAwait(false);
                InsertEvents(context, events, correlationId, contributor);
                await UpdateUniqueIndexedProperties<T>(context, sourceId, events, cancellationToken).ConfigureAwait(false);
                InsertCorrelation(sourceId, correlationId, context);

                await SaveChanges<T>(context, sourceId, correlationId, cancellationToken);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "This method is extracted.")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task UpsertAggregate<T>(
            EventStoreDbContext context,
            Guid sourceId,
            List<IDomainEvent> events,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            Aggregate aggregate = await context
                .Aggregates
                .Where(a => a.AggregateId == sourceId)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (aggregate == null)
            {
                aggregate = new Aggregate
                {
                    AggregateId = sourceId,
                    AggregateType = typeof(T).FullName,
                    Version = 0
                };
                context.Aggregates.Add(aggregate);
            }

            if (events.First().Version != aggregate.Version + 1)
            {
                throw new ArgumentException(
                    $"Version of the first of {nameof(events)} must follow aggregate.",
                    nameof(events));
            }

            aggregate.Version = events.Last().Version;
        }

        private void InsertEvents(
            EventStoreDbContext context,
            List<IDomainEvent> events,
            Guid? correlationId,
            string contributor)
        {
            foreach (IDomainEvent domainEvent in events)
            {
                InsertEvent(context, domainEvent, correlationId, contributor);
            }
        }

        private void InsertEvent(
            EventStoreDbContext context,
            IDomainEvent domainEvent,
            Guid? correlationId,
            string contributor)
        {
            var envelope = new Envelope(Guid.NewGuid(), domainEvent, correlationId: correlationId, contributor: contributor);
            context.PersistentEvents.Add(PersistentEvent.FromEnvelope(envelope, _serializer));
            context.PendingEvents.Add(PendingEvent.FromEnvelope(envelope, _serializer));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "This method is extracted.")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task UpdateUniqueIndexedProperties<T>(
            EventStoreDbContext context,
            Guid sourceId,
            List<IDomainEvent> events,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            Dictionary<string, UniqueIndexedProperty> restored = await context
                .UniqueIndexedProperties
                .Where(
                    p =>
                    p.AggregateType == typeof(T).FullName &&
                    p.AggregateId == sourceId)
                .ToDictionaryAsync(p => p.PropertyName, cancellationToken)
                .ConfigureAwait(false);

            var properties = new List<UniqueIndexedProperty>();

            foreach (var indexedEvent in events.OfType<IUniqueIndexedDomainEvent>())
            {
                foreach (string name in
                    indexedEvent.UniqueIndexedProperties.Keys)
                {
                    if (restored.TryGetValue(name, out UniqueIndexedProperty property))
                    {
                        context.UniqueIndexedProperties.Remove(property);
                    }

                    property = properties.Find(p => p.PropertyName == name);
                    if (property != null)
                    {
                        properties.Remove(property);
                    }

                    string value = indexedEvent.UniqueIndexedProperties[name];
                    if (value == null)
                    {
                        continue;
                    }

                    property = new UniqueIndexedProperty
                    {
                        AggregateType = typeof(T).FullName,
                        PropertyName = name,
                        PropertyValue = value,
                        AggregateId = sourceId,
                        Version = indexedEvent.Version
                    };
                    properties.Add(property);
                }
            }

            context.UniqueIndexedProperties.AddRange(properties);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "This method is extracted.")]
        private void InsertCorrelation(
            Guid sourceId,
            Guid? correlationId,
            EventStoreDbContext context)
        {
            if (correlationId == null)
            {
                return;
            }

            context.Correlations.Add(new Correlation
            {
                AggregateId = sourceId,
                CorrelationId = correlationId.Value,
                HandledAt = DateTimeOffset.Now
            });
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "This method is extracted.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "correlationId", Justification = "The parameter 'correlationId' is used on .NET Standard 2.0.")]
        private async Task SaveChanges<T>(
            EventStoreDbContext context,
            Guid sourceId,
            Guid? correlationId,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
#if NETSTANDARD2_0
            try
            {
                using (IDbContextTransaction transaction = await
                       context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
                {
                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    transaction.Commit();
                }
            }
            catch (DbUpdateException exception) when (correlationId.HasValue)
            {
                IQueryable<Correlation> query = from c in context.Correlations
                                                where c.AggregateId == sourceId && c.CorrelationId == correlationId
                                                select c;
                if (await query.AnyAsync().ConfigureAwait(false))
                {
                    throw new DuplicateCorrelationException(
                        typeof(T),
                        sourceId,
                        correlationId.Value,
                        exception);
                }

                throw;
            }
#else
            try
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException exception)
            when (exception.InnerException is UpdateException)
            {
                var updateException = (UpdateException)exception.InnerException;

                Correlation correlation = updateException
                    .StateEntries
                    .Select(s => s.Entity)
                    .OfType<Correlation>()
                    .FirstOrDefault();

                if (correlation != null)
                {
                    throw new DuplicateCorrelationException(
                        typeof(T),
                        sourceId,
                        correlation.CorrelationId,
                        exception);
                }

                throw;
            }
#endif
        }

        public Task<IEnumerable<IDomainEvent>> LoadEvents<T>(
            Guid sourceId,
            int afterVersion,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{sourceId} cannot be empty", nameof(sourceId));
            }

            return RunLoadEvents<T>(sourceId, afterVersion, cancellationToken);
        }

        private async Task<IEnumerable<IDomainEvent>> RunLoadEvents<T>(
            Guid sourceId,
            int afterVersion,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            using (EventStoreDbContext context = _dbContextFactory.Invoke())
            {
                List<PersistentEvent> persistentEvents = await context
                    .PersistentEvents
                    .Where(e => e.AggregateId == sourceId)
                    .Where(e => e.Version > afterVersion)
                    .OrderBy(e => e.Version)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                List<IDomainEvent> domainEvents = persistentEvents
                    .Select(e => e.EventJson)
                    .Select(_serializer.Deserialize)
                    .Cast<IDomainEvent>()
                    .ToList();

                return domainEvents;
            }
        }

        public Task<Guid?> FindIdByUniqueIndexedProperty<T>(
            string name,
            string value,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return FindIdByProperty<T>(name, value, cancellationToken);
        }

        private async Task<Guid?> FindIdByProperty<T>(
            string name,
            string value,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            using (EventStoreDbContext context = _dbContextFactory.Invoke())
            {
                IQueryable<UniqueIndexedProperty> query =
                    from p in context.UniqueIndexedProperties
                    where
                        p.AggregateType == typeof(T).FullName &&
                        p.PropertyName == name &&
                        p.PropertyValue == value
                    select p;

                UniqueIndexedProperty property = await query
                    .SingleOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                return property?.AggregateId;
            }
        }
    }
}
