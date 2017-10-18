namespace Khala.EventSourcing.Sql
{
    using FluentAssertions;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Xunit;

    public class EventStoreDbContext_specs
    {
        private readonly DbContextOptions _dbContextOptions;

        public EventStoreDbContext_specs()
        {
            _dbContextOptions = new DbContextOptionsBuilder()
                .UseInMemoryDatabase(nameof(EventStoreDbContext_specs))
                .Options;
        }

        [Fact]
        public void sut_inherits_DbContext()
        {
            typeof(EventStoreDbContext).BaseType.Should().Be(typeof(DbContext));
        }

        [Fact]
        public void model_has_Aggregate_entity()
        {
            var sut = new EventStoreDbContext(_dbContextOptions);
            IEntityType actual = sut.Model.FindEntityType(typeof(Aggregate));
            actual.Should().NotBeNull();
        }

        [Fact]
        public void Aggregate_entity_has_index_for_AggregateId()
        {
            var sut = new EventStoreDbContext(_dbContextOptions);
            IEntityType entity = sut.Model.FindEntityType(typeof(Aggregate));
            IIndex actual = entity.FindIndex(entity.FindProperty("AggregateId"));
            actual.Should().NotBeNull();
            actual.IsUnique.Should().BeTrue();
        }
    }
}
