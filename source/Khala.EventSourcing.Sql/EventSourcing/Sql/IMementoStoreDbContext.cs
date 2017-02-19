namespace Khala.EventSourcing.Sql
{
    using System;
    using System.Data.Entity;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IMementoStoreDbContext : IDisposable
    {
        DbSet<Memento> Mementoes { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}