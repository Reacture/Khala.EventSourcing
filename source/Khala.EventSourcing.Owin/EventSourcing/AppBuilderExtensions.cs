namespace Khala.EventSourcing
{
    using System;
    using System.Threading;
    using Microsoft.Owin.BuilderProperties;
    using Owin;

    public static class AppBuilderExtensions
    {
        [Obsolete("Use EnqueuePendingEvents(IAppBuilder, IEventPublisher) instead. This method will be removed in version 1.0.0.")]
        public static void EnqueuePendingEvents<T>(
            this IAppBuilder app,
            IEventSourcedRepository<T> repository)
            where T : class, IEventSourced
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            if (repository == null)
            {
                throw new ArgumentNullException(nameof(repository));
            }

            app.EnqueuePendingEvents(repository.EventPublisher);
        }

        public static void EnqueuePendingEvents(
            this IAppBuilder app,
            IEventPublisher eventPublisher)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            if (eventPublisher == null)
            {
                throw new ArgumentNullException(nameof(eventPublisher));
            }

            var appProperties = new AppProperties(app.Properties);
            CancellationToken cancellationToken = appProperties.OnAppDisposing;
            eventPublisher.EnqueueAll(cancellationToken);
        }
    }
}
