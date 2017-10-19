namespace Khala.EventSourcing.Sql
{
    using FluentAssertions;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Xunit;

    public class MementoStoreDbContext_specs
    {
        private readonly DbContextOptions _dbContextOptions;

        public MementoStoreDbContext_specs()
        {
            _dbContextOptions = new DbContextOptionsBuilder()
                .UseInMemoryDatabase(nameof(MementoStoreDbContext_specs))
                .Options;
        }

        [Fact]
        public void sut_inherits_DbContext()
        {
            typeof(MementoStoreDbContext).BaseType.Should().Be(typeof(DbContext));
        }

        [Fact]
        public void model_has_Memento_entity()
        {
            var sut = new MementoStoreDbContext(_dbContextOptions);
            IEntityType actual = sut.Model.FindEntityType(typeof(Memento));
            actual.Should().NotBeNull();
        }

        [Fact]
        public void Memento_entity_has_index_with_AggregateId()
        {
            var sut = new MementoStoreDbContext(_dbContextOptions);
            IEntityType entity = sut.Model.FindEntityType(typeof(Memento));
            IIndex actual = entity.FindIndex(entity.FindProperty("AggregateId"));
            actual.Should().NotBeNull();
            actual.IsUnique.Should().BeTrue();
        }
    }
}
