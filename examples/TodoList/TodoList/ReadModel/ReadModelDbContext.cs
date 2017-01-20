using System.Data.Entity;

namespace TodoList.ReadModel
{
    public class ReadModelDbContext : DbContext
    {
        public DbSet<TodoItem> TodoItems { get; set; }
    }
}
