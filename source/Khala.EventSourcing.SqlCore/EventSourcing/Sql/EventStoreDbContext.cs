namespace Khala.EventSourcing.Sql
{
    using Microsoft.EntityFrameworkCore;

    public class EventStoreDbContext : DbContext
    {
        public EventStoreDbContext(DbContextOptions options)
            : base(options)
        {
        }

        public DbSet<Aggregate> Aggregates { get; set; }

        public DbSet<PersistentEvent> PersistentEvents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Aggregate>().HasIndex(nameof(Aggregate.AggregateId)).IsUnique();
            modelBuilder.Entity<PersistentEvent>().HasIndex(nameof(PersistentEvent.AggregateId), nameof(PersistentEvent.Version)).IsUnique();
        }
    }
}
