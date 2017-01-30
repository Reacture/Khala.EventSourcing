namespace Khala.EventSourcing.Sql
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ISqlEventPublisher : IEventPublisher
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "As designed.")]
        Task PublishPendingEvents<T>(
            Guid sourceId,
            CancellationToken cancellationToken)
            where T : class, IEventSourced;
    }
}
