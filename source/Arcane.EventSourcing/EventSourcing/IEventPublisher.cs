namespace Arcane.EventSourcing
{
    using System.Threading;

    public interface IEventPublisher
    {
        void EnqueueAll(CancellationToken cancellationToken);
    }
}
