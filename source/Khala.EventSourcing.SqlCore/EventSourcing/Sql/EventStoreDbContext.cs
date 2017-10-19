namespace Khala.EventSourcing.Sql
{
#if NETSTANDARD2_0
    using Microsoft.EntityFrameworkCore;
#else
    using System.Data.Entity;
#endif

    public class EventStoreDbContext : DbContext
    {
#if NETSTANDARD2_0
        public EventStoreDbContext(DbContextOptions options)
            : base(options)
        {
        }
#else
        public EventStoreDbContext()
        {
        }

        public EventStoreDbContext(string nameOrConnectionString)
            : base(nameOrConnectionString)
        {
        }
#endif

        public DbSet<Aggregate> Aggregates { get; set; }

        public DbSet<PersistentEvent> PersistentEvents { get; set; }

        public DbSet<PendingEvent> PendingEvents { get; set; }

        public DbSet<UniqueIndexedProperty> UniqueIndexedProperties { get; set; }

        public DbSet<Correlation> Correlations { get; set; }

#if NETSTANDARD2_0
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Aggregate>().HasIndex(nameof(Aggregate.AggregateId)).IsUnique();
            modelBuilder.Entity<PersistentEvent>().HasIndex(nameof(PersistentEvent.AggregateId), nameof(PersistentEvent.Version)).IsUnique();
            modelBuilder.Entity<PendingEvent>().HasKey(nameof(PendingEvent.AggregateId), nameof(PendingEvent.Version));
            modelBuilder.Entity<UniqueIndexedProperty>().HasKey(nameof(UniqueIndexedProperty.AggregateType), nameof(UniqueIndexedProperty.AggregateId), nameof(UniqueIndexedProperty.PropertyValue));
            modelBuilder.Entity<UniqueIndexedProperty>().HasIndex(nameof(UniqueIndexedProperty.AggregateId), nameof(UniqueIndexedProperty.PropertyName)).IsUnique();
            modelBuilder.Entity<Correlation>().HasKey(nameof(Correlation.AggregateId), nameof(Correlation.CorrelationId));
        }
#endif
    }
}
