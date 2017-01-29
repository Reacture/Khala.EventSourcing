namespace Arcane.EventSourcing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IMementoStore
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "As designed.")]
        Task Save<T>(
            Guid sourceId,
            IMemento memento,
            CancellationToken cancellationToken)
            where T : class, IEventSourced;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "As designed.")]
        Task<IMemento> Find<T>(
            Guid sourceId,
            CancellationToken cancellationToken)
            where T : class, IEventSourced;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "As designed.")]
        Task Delete<T>(
            Guid sourceId,
            CancellationToken cancellationToken)
            where T : class, IEventSourced;
    }
}
