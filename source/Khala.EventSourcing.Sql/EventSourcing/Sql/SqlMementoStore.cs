namespace Khala.EventSourcing.Sql
{
    using System;
    using System.Data.Entity;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Messaging;

    public class SqlMementoStore : IMementoStore
    {
        private readonly Func<MementoStoreDbContext> _dbContextFactory;
        private readonly IMessageSerializer _serializer;

        public SqlMementoStore(
            Func<MementoStoreDbContext> dbContextFactory,
            IMessageSerializer serializer)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public Task Save<T>(
            Guid sourceId,
            IMemento memento,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{nameof(sourceId)} cannot be empty.", nameof(sourceId));
            }

            if (memento == null)
            {
                throw new ArgumentNullException(nameof(memento));
            }

            return SaveMemento(sourceId, memento, cancellationToken);
        }

        private async Task SaveMemento(
            Guid sourceId,
            IMemento memento,
            CancellationToken cancellationToken)
        {
            using (MementoStoreDbContext context = _dbContextFactory.Invoke())
            {
                Memento entity = await context
                    .Mementoes
                    .Where(m => m.AggregateId == sourceId)
                    .SingleOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (entity == null)
                {
                    entity = new Memento { AggregateId = sourceId };
                    context.Mementoes.Add(entity);
                }

                entity.MementoJson = _serializer.Serialize(memento);

                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public Task<IMemento> Find<T>(
            Guid sourceId, CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{nameof(sourceId)} cannot be empty.", nameof(sourceId));
            }

            return FindMemento(sourceId, cancellationToken);
        }

        private async Task<IMemento> FindMemento(
            Guid sourceId,
            CancellationToken cancellationToken)
        {
            using (MementoStoreDbContext context = _dbContextFactory.Invoke())
            {
                Memento entity = await context
                    .Mementoes
                    .Where(m => m.AggregateId == sourceId)
                    .SingleOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (entity == null)
                {
                    return null;
                }

                object memento = _serializer.Deserialize(entity.MementoJson);

                return (IMemento)memento;
            }
        }

        public Task Delete<T>(
            Guid sourceId,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{nameof(sourceId)} cannot be empty.", nameof(sourceId));
            }

            return DeleteMemento(sourceId, cancellationToken);
        }

        private async Task DeleteMemento(
            Guid sourceId,
            CancellationToken cancellationToken)
        {
            using (MementoStoreDbContext context = _dbContextFactory.Invoke())
            {
                Memento entity = await context
                    .Mementoes
                    .Where(m => m.AggregateId == sourceId)
                    .SingleOrDefaultAsync()
                    .ConfigureAwait(false);

                if (entity == null)
                {
                    return;
                }

                context.Mementoes.Remove(entity);

                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
