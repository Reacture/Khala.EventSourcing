namespace Khala.EventSourcing.Sql
{
#if NETSTANDARD2_0
    using Microsoft.EntityFrameworkCore;
#else
    using System.Data.Entity;
#endif

    public class MementoStoreDbContext : DbContext
    {
#if NETSTANDARD2_0
        public MementoStoreDbContext(DbContextOptions options)
            : base(options)
        {
        }
#else
        public MementoStoreDbContext()
        {
        }

        public MementoStoreDbContext(string nameOrConnectionString)
            : base(nameOrConnectionString)
        {
        }
#endif

        public DbSet<Memento> Mementoes { get; set; }

#if NETSTANDARD2_0
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Memento>().HasIndex(nameof(Memento.AggregateId)).IsUnique();
        }
#endif
    }
}
