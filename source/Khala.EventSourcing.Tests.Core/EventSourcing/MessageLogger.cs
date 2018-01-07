namespace Khala.EventSourcing
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Khala.Messaging;

    public class MessageLogger : IMessageBus
    {
        private readonly ConcurrentQueue<Envelope> _log = new ConcurrentQueue<Envelope>();

        public IEnumerable<Envelope> Log => _log;

        public Task Send(Envelope envelope, CancellationToken cancellationToken)
        {
            _log.Enqueue(envelope);

            return Task.CompletedTask;
        }

        public Task Send(IEnumerable<Envelope> envelopes, CancellationToken cancellationToken)
        {
            foreach (Envelope envelope in envelopes)
            {
                _log.Enqueue(envelope);
            }

            return Task.CompletedTask;
        }
    }
}
