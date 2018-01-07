namespace Khala.EventSourcing
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Khala.Messaging;

    public class CompletableMessageBus : IMessageBus
    {
        private readonly TaskCompletionSource<bool> _completionSource = new TaskCompletionSource<bool>();

        public void Complete() => _completionSource.SetResult(true);

        public Task Send(Envelope envelope, CancellationToken cancellationToken) => _completionSource.Task;

        public Task Send(IEnumerable<Envelope> envelopes, CancellationToken cancellationToken) => _completionSource.Task;
    }
}
