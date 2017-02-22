using System.Data.Entity;
using Khala.EventSourcing.Sql;

namespace TodoList.Domain.DataAccess
{
    public class TodoListEventStoreDbContext :
        EventStoreDbContext,
        IMementoStoreDbContext
    {
        public DbSet<Memento> Mementoes { get; set; }
    }
}
