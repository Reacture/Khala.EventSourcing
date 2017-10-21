namespace Khala.FakeDomain
{
    using Khala.EventSourcing.Sql;
    using Microsoft.EntityFrameworkCore;

    public class FakeEventStoreDbContext : EventStoreDbContext
    {
        public FakeEventStoreDbContext(DbContextOptions options)
            : base(options)
        {
        }

        public FakeEventStoreDbContext()
            : this(new DbContextOptionsBuilder()
                .UseSqlServer($@"Server=(localdb)\mssqllocaldb;Database={typeof(FakeEventStoreDbContext).FullName};Trusted_Connection=True;")
                .Options)
        {
        }
    }
}
