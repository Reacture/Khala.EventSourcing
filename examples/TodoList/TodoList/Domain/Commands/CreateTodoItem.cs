using System;

namespace TodoList.Domain.Commands
{
    public class CreateTodoItem : TodoItemCommand
    {
        public CreateTodoItem(Guid todoItemId, string description)
            : base(todoItemId)
        {
            if (description == null)
                throw new ArgumentNullException(nameof(description));

            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException(
                    $"{nameof(description)} cannot be empty.",
                    nameof(description));
            }

            Description = description;
        }

        public string Description { get; }
    }
}
