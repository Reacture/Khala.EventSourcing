namespace ReactiveArchitecture.Messaging
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IMessageBus
    {
        Task Send(Envelope message);

        Task SendBatch(IEnumerable<Envelope> messages);
    }
}
