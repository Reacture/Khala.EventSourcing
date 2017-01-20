using System;

namespace TodoList.Domain.Commands
{
    public abstract class TodoItemCommand
    {
        protected TodoItemCommand(Guid todoItemId)
        {
            if (todoItemId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{nameof(todoItemId)} cannot be empty.",
                    nameof(todoItemId));
            }

            TodoItemId = todoItemId;
        }

        public Guid TodoItemId { get; }
    }
}
