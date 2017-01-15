﻿namespace ReactiveArchitecture.EventSourcing.Sql
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ISqlEventPublisher
    {
        Task PublishPendingEvents<T>(
            Guid sourceId,
            CancellationToken cancellationToken)
            where T : class, IEventSourced;

        void EnqueueAll(CancellationToken cancellationToken);
    }
}
