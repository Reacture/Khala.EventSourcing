namespace ReactiveArchitecture.Messaging
{
    using System;
    using System.Collections.Generic;

    public class Envelope
    {
        public Envelope(
            object message,
            IReadOnlyDictionary<string, object> properties)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (properties == null)
            {
                throw new ArgumentNullException(nameof(properties));
            }

            Message = message;
            Properties = properties;
        }

        public object Message { get; }

        public IReadOnlyDictionary<string, object> Properties { get; }
    }
}
