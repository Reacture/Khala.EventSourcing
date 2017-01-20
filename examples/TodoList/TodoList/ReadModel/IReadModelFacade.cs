using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TodoList.ReadModel
{
    public interface IReadModelFacade
    {
        Task<IEnumerable<TodoItem>> GetAllItems();

        Task<TodoItem> Find(Guid id);
    }
}
