using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Arcane.Messaging;

namespace TodoList.Messaging
{
    public class ImmediateMessageBus : IMessageBus
    {
        private readonly Lazy<IMessageHandler> _messageHandler;

        public ImmediateMessageBus(Lazy<IMessageHandler> messageHandler)
        {
            if (messageHandler == null)
                throw new ArgumentNullException(nameof(messageHandler));

            _messageHandler = messageHandler;
        }

        public async Task Send(
            Envelope envelope,
            CancellationToken cancellationToken)
        {
            IMessageHandler handler = _messageHandler.Value;
            try
            {
                await handler.Handle(envelope, cancellationToken);
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
            }
        }

        public async Task SendBatch(
            IEnumerable<Envelope> envelopes,
            CancellationToken cancellationToken)
        {
            IMessageHandler handler = _messageHandler.Value;
            foreach (Envelope envelope in envelopes)
            {
                try
                {
                    await handler.Handle(envelope, cancellationToken);
                }
                catch (Exception exception)
                {
                    Trace.TraceError(exception.ToString());
                }
            }
        }
    }
}
