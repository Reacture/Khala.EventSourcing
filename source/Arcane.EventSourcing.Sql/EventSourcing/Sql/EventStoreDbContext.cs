namespace Arcane.EventSourcing.Sql
{
    using System.Data.Entity;

    public class EventStoreDbContext : DbContext
    {
        public EventStoreDbContext()
        {
        }

        public EventStoreDbContext(string nameOrConnectionString)
            : base(nameOrConnectionString)
        {
        }

        public DbSet<Aggregate> Aggregates { get; set; }

        public DbSet<PersistentEvent> PersistentEvents { get; set; }

        public DbSet<PendingEvent> PendingEvents { get; set; }

        public DbSet<UniqueIndexedProperty> UniqueIndexedProperties { get; set; }
    }
}
