using System;
using System.Threading;
using System.Threading.Tasks;
using Arcane.EventSourcing;
using Arcane.Messaging;
using TodoList.Domain.Commands;

namespace TodoList.Domain
{
    public class TodoItemCommandHandler :
        ExplicitMessageHandler,
        IHandles<CreateTodoItem>,
        IHandles<UpdateTodoItem>,
        IHandles<DeleteTodoItem>
    {
        private readonly IEventSourcedRepository<TodoItem> _repository;

        public TodoItemCommandHandler(
            IEventSourcedRepository<TodoItem> repository)
        {
            if (repository == null)
                throw new ArgumentNullException(nameof(repository));

            _repository = repository;
        }

        public Task Handle(
            ReceivedEnvelope<CreateTodoItem> envelope,
            CancellationToken cancellationToken)
        {
            CreateTodoItem command = envelope.Message;
            Guid messageId = envelope.MessageId;

            var todoItem = new TodoItem(
                command.TodoItemId,
                command.Description);

            return _repository.Save(todoItem, messageId, cancellationToken);
        }

        public async Task Handle(
            ReceivedEnvelope<UpdateTodoItem> envelope,
            CancellationToken cancellationToken)
        {
            UpdateTodoItem command = envelope.Message;
            Guid messageId = envelope.MessageId;

            TodoItem todoItem = await
                _repository.Find(command.TodoItemId, cancellationToken);

            if (todoItem == null)
            {
                throw new InvalidOperationException(
                    $"Cannot find todo item with id '{command.TodoItemId}'.");
            }

            todoItem.Update(command.Description);

            await _repository.Save(todoItem, messageId, cancellationToken);
        }

        public async Task Handle(
            ReceivedEnvelope<DeleteTodoItem> envelope,
            CancellationToken cancellationToken)
        {
            DeleteTodoItem command = envelope.Message;
            Guid messageId = envelope.MessageId;

            TodoItem todoItem = await
                _repository.Find(command.TodoItemId, cancellationToken);

            if (todoItem == null)
            {
                throw new InvalidOperationException(
                    $"Cannot find todo item with id '{command.TodoItemId}'.");
            }

            todoItem.Delete();

            await _repository.Save(todoItem, messageId, cancellationToken);
        }
    }
}
