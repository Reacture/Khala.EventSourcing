using System;

namespace TodoList.Domain.Commands
{
    public class DeleteTodoItem : TodoItemCommand
    {
        public DeleteTodoItem(Guid todoItemId)
            : base(todoItemId)
        {
        }
    }
}
