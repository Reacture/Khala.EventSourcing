namespace Khala.EventSourcing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Khala.Messaging;

    public static class EventSourcingExtensions
    {
        public static Task SaveAndPublish<T>(
            this IEventSourcedRepository<T> repository,
            T source,
            IEnvelope correlation,
            CancellationToken cancellationToken = default)
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

            if (correlation == null)
            {
                throw new ArgumentNullException(nameof(correlation));
            }

            return repository.SaveAndPublish(
                source,
                correlation.OperationId,
                correlation.MessageId,
                correlation.Contributor,
                cancellationToken);
        }
    }
}
