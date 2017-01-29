namespace Arcane.EventSourcing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public static class EventSourcingExtensions
    {
        public static Task Save<T>(
            this IEventSourcedRepository<T> repository,
            T source,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            if (repository == null)
            {
                throw new ArgumentNullException(nameof(repository));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return repository.Save(source, null, cancellationToken);
        }
    }
}
