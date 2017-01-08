namespace ReactiveArchitecture.Messaging
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IMessageBus
    {
        Task SendBatch(IEnumerable<object> messages);
    }
}
