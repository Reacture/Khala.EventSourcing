using System;
using Khala.EventSourcing;
using Khala.EventSourcing.Sql;
using Khala.Messaging;
using Microsoft.Owin;
using Microsoft.Owin.BuilderProperties;
using Owin;
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

            IEventSourcedRepository<Domain.TodoItem> repository =
                new SqlEventSourcedRepository<Domain.TodoItem>(
                    () => new TodoListEventStoreDbContext(),
                    new JsonMessageSerializer(),
                    messageBus,
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
            repository.EventPublisher.EnqueueAll(properties.OnAppDisposing);
        }
    }
}
