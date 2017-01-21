using System;
using Microsoft.Owin;
using Microsoft.Owin.BuilderProperties;
using Owin;
using ReactiveArchitecture.EventSourcing;
using ReactiveArchitecture.EventSourcing.Sql;
using ReactiveArchitecture.Messaging;
using TodoList.Domain;
using TodoList.Domain.DataAccess;
using TodoList.Messaging;
using TodoList.ReadModel;

[assembly: OwinStartup(typeof(TodoList.Startup))]

namespace TodoList
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            IMessageHandler messageHandler = null;

            IMessageBus messageBus = new ImmediateMessageBus(
                new Lazy<IMessageHandler>(() => messageHandler));

            var serializer = new JsonMessageSerializer();

            var eventStore = new SqlEventStore(
                () => new TodoListEventStoreDbContext(),
                serializer);

            var eventPublisher = new SqlEventPublisher(
                () => new TodoListEventStoreDbContext(),
                serializer,
                messageBus);

            IEventSourcedRepository<Domain.TodoItem> repository =
                new SqlEventSourcedRepository<Domain.TodoItem>(
                    eventStore,
                    eventPublisher,
                    Domain.TodoItem.Factory);

            messageHandler = new CompositeMessageHandler(
                new TodoItemCommandHandler(repository),
                new ReadModelGenerator(() => new ReadModelDbContext()));

            IReadModelFacade readModelFacade =
                new ReadModelFacade(() => new ReadModelDbContext());

            app.Use(async (context, next) =>
            {
                context.Set(nameof(IMessageBus), messageBus);
                context.Set(nameof(IReadModelFacade), readModelFacade);

                await next.Invoke();
            });

            var properties = new AppProperties(app.Properties);
            eventPublisher.EnqueueAll(properties.OnAppDisposing);
        }
    }
}
