namespace Khala.EventSourcing.Sql
{
    using System.Data.Entity;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1063:ImplementIDisposableCorrectly", Justification = "As designed.")]
    public class MementoStoreDbContext : DbContext
    {
        public DbSet<Memento> Mementoes { get; set; }
    }
}
