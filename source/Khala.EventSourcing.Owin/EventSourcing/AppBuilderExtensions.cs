using System;
using System.Threading;
using Microsoft.Owin.BuilderProperties;
using Owin;

namespace Khala.EventSourcing
{
    public static class AppBuilderExtensions
    {
        public static void EnqueuePendingEvents<T>(
            this IAppBuilder app,
            IEventSourcedRepository<T> repository)
            where T : class, IEventSourced
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));

            if (repository == null)
                throw new ArgumentNullException(nameof(repository));

            var appProperties = new AppProperties(app.Properties);
            CancellationToken cancellationToken = appProperties.OnAppDisposing;
            repository.EventPublisher.EnqueueAll(cancellationToken);
        }
    }
}
