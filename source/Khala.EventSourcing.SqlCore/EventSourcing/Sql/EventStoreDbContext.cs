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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Aggregate>().HasIndex(nameof(Aggregate.AggregateId)).IsUnique();
        }
    }
}
