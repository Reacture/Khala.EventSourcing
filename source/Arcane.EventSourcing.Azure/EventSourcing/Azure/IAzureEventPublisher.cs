namespace Arcane.EventSourcing.Azure
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IAzureEventPublisher : IEventPublisher
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "As designed.")]
        Task PublishPendingEvents<T>(
            Guid sourceId,
            CancellationToken cancellationToken)
            where T : class, IEventSourced;
    }
}
