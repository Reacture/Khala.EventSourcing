namespace ReactiveArchitecture.Messaging
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IMessageBus
    {
        Task Send(object message);

        Task SendBatch(IEnumerable<object> messages);
    }
}
