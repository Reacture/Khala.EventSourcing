﻿namespace ReactiveArchitecture.EventSourcing.Azure
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IAzureEventPublisher
    {
        Task PublishPendingEvents<T>(
            Guid sourceId, CancellationToken cancellationToken = default(CancellationToken))
            where T : class, IEventSourced;
    }
}