using System;
using System.Diagnostics;
using Khala.EventSourcing;
using Khala.EventSourcing.Sql;
using Khala.Messaging;
using Microsoft.Owin;
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

            var serializer = new JsonMessageSerializer();

            Func<TodoListEventStoreDbContext> dbContextFactory = () =>
            {
                var context = new TodoListEventStoreDbContext();
                context.Database.Log += m => Debug.WriteLine(m);
                return context;
            };

            IEventSourcedRepository<Domain.TodoItem> repository =
                new SqlEventSourcedRepository<Domain.TodoItem>(
                    new SqlEventStore(
                        dbContextFactory,
                        serializer),
                    new SqlEventPublisher(
                        dbContextFactory,
                        serializer,
                        messageBus),
                    new SqlMementoStore(
                        dbContextFactory,
                        serializer),
                    Domain.TodoItem.Factory,
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

            app.EnqueuePendingEvents(repository);
        }
    }
}
