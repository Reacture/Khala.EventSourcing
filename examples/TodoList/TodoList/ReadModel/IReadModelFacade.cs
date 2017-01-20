using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TodoList.ReadModel
{
    public interface IReadModelFacade
    {
        Task<IEnumerable<TodoItem>> GetAllTodoItems();

        Task<TodoItem> FindTodoItem(Guid id);
    }
}
