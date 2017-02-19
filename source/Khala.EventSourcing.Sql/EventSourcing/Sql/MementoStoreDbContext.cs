namespace Khala.EventSourcing.Sql
{
    using System.Data.Entity;

    public class MementoStoreDbContext : DbContext, IMementoStoreDbContext
    {
        public DbSet<Memento> Mementoes { get; set; }
    }
}
