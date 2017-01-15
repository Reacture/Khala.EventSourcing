namespace ReactiveArchitecture.EventSourcing.Messaging
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IMessageBus
    {
        Task SendBatch(
            IEnumerable<object> messages,
            CancellationToken cancellationToken);
    }
}
