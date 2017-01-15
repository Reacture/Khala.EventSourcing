namespace ReactiveArchitecture.EventSourcing.Azure
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IAzureEventCorrector
    {
        Task CorrectEvents<T>(
            Guid sourceId,
            CancellationToken cancellationToken = default(CancellationToken))
            where T : class, IEventSourced;
    }
}
