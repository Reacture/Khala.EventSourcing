using System;
using System.Threading;
using System.Threading.Tasks;
using ReactiveArchitecture.EventSourcing;
using ReactiveArchitecture.Messaging;
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
            CreateTodoItem command,
            CancellationToken cancellationToken)
        {
            var todoItem = new TodoItem(
                command.TodoItemId,
                command.Description);

            return _repository.Save(todoItem, cancellationToken);
        }

        public async Task Handle(
            UpdateTodoItem command,
            CancellationToken cancellationToken)
        {
            TodoItem todoItem = await
                _repository.Find(command.TodoItemId, cancellationToken);

            if (todoItem == null)
            {
                throw new InvalidOperationException(
                    $"Cannot find todo item with id '{command.TodoItemId}'.");
            }

            todoItem.Update(command.Description);

            await _repository.Save(todoItem, cancellationToken);
        }

        public async Task Handle(
            DeleteTodoItem command,
            CancellationToken cancellationToken)
        {
            TodoItem todoItem = await
                _repository.Find(command.TodoItemId, cancellationToken);

            if (todoItem == null)
            {
                throw new InvalidOperationException(
                    $"Cannot find todo item with id '{command.TodoItemId}'.");
            }

            todoItem.Delete();

            await _repository.Save(todoItem, cancellationToken);
        }
    }
}
