using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ReactiveArchitecture.Messaging;

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
            object message,
            CancellationToken cancellationToken)
        {
            IMessageHandler handler = _messageHandler.Value;
            try
            {
                await handler.Handle(message, cancellationToken);
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
            }
        }

        public async Task SendBatch(
            IEnumerable<object> messages,
            CancellationToken cancellationToken)
        {
            IMessageHandler handler = _messageHandler.Value;
            foreach (object message in messages)
            {
                try
                {
                    await handler.Handle(message, cancellationToken);
                }
                catch (Exception exception)
                {
                    Trace.TraceError(exception.ToString());
                }
            }
        }
    }
}
