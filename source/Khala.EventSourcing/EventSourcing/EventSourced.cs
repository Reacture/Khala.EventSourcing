namespace Khala.EventSourcing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Provices the abstract base class for event sourcing applied aggregate.
    /// </summary>
    public abstract class EventSourced : IEventSourced
    {
        private readonly Guid _id;
        private readonly Dictionary<Type, Action<IDomainEvent>> _eventHandlers;
        private readonly List<IDomainEvent> _pendingEvents;
        private int _version;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventSourced"/> class with the identifier of the aggregate.
        /// </summary>
        /// <param name="id">The identifier of the aggregate.</param>
        protected EventSourced(Guid id)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{nameof(id)} cannot be empty", nameof(id));
            }

            _id = id;
            _eventHandlers = new Dictionary<Type, Action<IDomainEvent>>();
            _pendingEvents = new List<IDomainEvent>();
            _version = 0;

            WireupEvents();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventSourced"/> class with the identifier of the aggregate and the memento.
        /// </summary>
        /// <param name="id">The identifier of the aggregate.</param>
        /// <param name="memento">The memento that contains snapshot data.</param>
        protected EventSourced(Guid id, IMemento memento)
            : this(id)
        {
            if (memento == null)
            {
                throw new ArgumentNullException(nameof(memento));
            }

            _version = memento.Version;
        }

        /// <summary>
        /// Encapsulates a strongly typed domain event handler method.
        /// </summary>
        /// <typeparam name="TEvent">The type of the domain event.</typeparam>
        /// <param name="domainEvent">A domain event instance.</param>
        protected delegate void DomainEventHandler<TEvent>(TEvent domainEvent)
            where TEvent : IDomainEvent;

        /// <summary>
        /// Gets the identifier of the aggregate.
        /// </summary>
        /// <value>
        /// The identifier of the aggregate.
        /// </value>
        public Guid Id => _id;

        /// <summary>
        /// Gets the version of the aggregate.
        /// </summary>
        /// <value>
        /// The version of the aggregate.
        /// </value>
        public int Version => _version;

        /// <summary>
        /// Gets the sequence of domain events raised after the aggregate is created or restored.
        /// </summary>
        /// <value>
        /// The sequence of domain events raised after the aggregate is created or restored.
        /// </value>
        public IEnumerable<IDomainEvent> PendingEvents => _pendingEvents;

        private void WireupEvents()
        {
            var handlers =
                from m in GetType().GetTypeInfo().GetDeclaredMethods("Handle")
                where m.ReturnType == typeof(void)
                let parameters = m.GetParameters()
                where parameters.Length == 1
                let parameter = parameters.Single()
                let parameterType = parameter.ParameterType
                where typeof(IDomainEvent).GetTypeInfo().IsAssignableFrom(parameterType.GetTypeInfo())
                select new { EventType = parameterType, Method = m };

            foreach (var handler in handlers)
            {
                WireupEvent(handler.EventType, handler.Method);
            }
        }

        private void WireupEvent(Type eventType, MethodInfo method)
        {
            MethodInfo template = typeof(EventSourced)
                .GetTypeInfo()
                .GetDeclaredMethod("WireupEventHander");
            MethodInfo wireup = template.MakeGenericMethod(eventType);
            wireup.Invoke(this, new[] { method });
        }

        private void WireupEventHander<TEvent>(MethodInfo method)
            where TEvent : IDomainEvent
        {
            SetEventHandler(CreateDelegate<DomainEventHandler<TEvent>>(method));
        }

        private TDelegate CreateDelegate<TDelegate>(MethodInfo method)
            where TDelegate : class
        {
            return (TDelegate)(object)method.CreateDelegate(typeof(TDelegate), this);
        }

        /// <summary>
        /// Registers a strongly typed domain event handler explicitly.
        /// </summary>
        /// <typeparam name="TEvent">The type of the domain event.</typeparam>
        /// <param name="handler">A domain event handler function that handles events of the type <typeparamref name="TEvent"/>.</param>
        protected void SetEventHandler<TEvent>(
            DomainEventHandler<TEvent> handler)
            where TEvent : IDomainEvent
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (_eventHandlers.ContainsKey(typeof(TEvent)))
            {
                throw new InvalidOperationException(
                    $"{typeof(TEvent).FullName} handler already connected.");
            }

            _eventHandlers.Add(typeof(TEvent), e => handler.Invoke((TEvent)e));
        }

        /// <summary>
        /// Handles past domain events sequentially. This method is generally used by constructors or factory methods.
        /// </summary>
        /// <param name="pastEvents">A sequence that contains domain events already raised in the past.</param>
        protected void HandlePastEvents(IEnumerable<IDomainEvent> pastEvents)
        {
            if (pastEvents == null)
            {
                throw new ArgumentNullException(nameof(pastEvents));
            }

            foreach (IDomainEvent domainEvent in pastEvents)
            {
                if (domainEvent == null)
                {
                    throw new ArgumentException(
                        $"{nameof(pastEvents)} cannot contain null.",
                        nameof(pastEvents));
                }

                try
                {
                    HandleEvent(domainEvent);
                }
                catch (ArgumentException exception)
                {
                    var message =
                        $"Could not handle {nameof(pastEvents)} successfully." +
                        " See the inner exception for details.";
                    throw new ArgumentException(
                        message, nameof(pastEvents), exception);
                }
            }
        }

        /// <summary>
        /// Raises and handles a domain event.
        /// </summary>
        /// <typeparam name="TEvent">The type of the domain event.</typeparam>
        /// <param name="domainEvent">A domain event instance that will be raised and handled by the aggregate.</param>
        /// <remarks><paramref name="domainEvent"/> is added to <see cref="PendingEvents"/> after handled.</remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate", Justification = "RaiseEvent<TEvent>() method does not follow .NET event pattern but follows event sourcing.")]
        protected void RaiseEvent<TEvent>(TEvent domainEvent)
            where TEvent : IDomainEvent
        {
            if (domainEvent == null)
            {
                throw new ArgumentNullException(nameof(domainEvent));
            }

            domainEvent.Raise(this);
            HandleEvent(domainEvent);
            _pendingEvents.Add(domainEvent);
        }

        private void HandleEvent(IDomainEvent domainEvent)
        {
            if (domainEvent.SourceId != _id)
            {
                var message = $"{nameof(domainEvent.SourceId)} is invalid.";
                throw new ArgumentException(message, nameof(domainEvent));
            }

            if (domainEvent.Version != _version + 1)
            {
                var message = $"{nameof(domainEvent.Version)} is invalid.";
                throw new ArgumentException(message, nameof(domainEvent));
            }

            Type eventType = domainEvent.GetType();
            Action<IDomainEvent> handler;
            if (_eventHandlers.TryGetValue(eventType, out handler))
            {
                handler.Invoke(domainEvent);
                _version = domainEvent.Version;
            }
            else
            {
                var message = $"Cannot handle event of type {eventType}.";
                throw new InvalidOperationException(message);
            }
        }
    }
}
