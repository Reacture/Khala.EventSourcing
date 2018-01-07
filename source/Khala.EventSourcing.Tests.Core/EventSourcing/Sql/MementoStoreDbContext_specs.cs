namespace Khala.EventSourcing.Sql
{
    using FluentAssertions;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class MementoStoreDbContext_specs
    {
        private DbContextOptions _dbContextOptions;

        [TestInitialize]
        public void TestInitialize()
        {
            _dbContextOptions = new DbContextOptionsBuilder()
                .UseInMemoryDatabase(nameof(MementoStoreDbContext_specs))
                .Options;
        }

        [TestMethod]
        public void sut_inherits_DbContext()
        {
            typeof(MementoStoreDbContext).BaseType.Should().Be(typeof(DbContext));
        }

        [TestMethod]
        public void model_has_Memento_entity()
        {
            var sut = new MementoStoreDbContext(_dbContextOptions);
            IEntityType actual = sut.Model.FindEntityType(typeof(Memento));
            actual.Should().NotBeNull();
        }

        [TestMethod]
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
