namespace Khala.EventSourcing.Sql
{
    using FluentAssertions;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EventStoreDbContext_specs
    {
        private DbContextOptions _dbContextOptions;

        [TestInitialize]
        public void TestInitialize()
        {
            _dbContextOptions = new DbContextOptionsBuilder()
                .UseInMemoryDatabase(nameof(EventStoreDbContext_specs))
                .Options;
        }

        [TestMethod]
        public void sut_inherits_DbContext()
        {
            typeof(EventStoreDbContext).BaseType.Should().Be(typeof(DbContext));
        }

        [TestMethod]
        public void model_has_Aggregate_entity()
        {
            var sut = new EventStoreDbContext(_dbContextOptions);
            IEntityType actual = sut.Model.FindEntityType(typeof(Aggregate));
            actual.Should().NotBeNull();
        }

        [TestMethod]
        public void Aggregate_entity_has_index_for_AggregateId()
        {
            var context = new EventStoreDbContext(_dbContextOptions);
            IEntityType sut = context.Model.FindEntityType(typeof(Aggregate));
            IIndex actual = sut.FindIndex(sut.FindProperty("AggregateId"));
            actual.Should().NotBeNull();
            actual.IsUnique.Should().BeTrue();
        }
    }
}
