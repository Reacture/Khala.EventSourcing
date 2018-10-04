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
        public void Aggregate_entity_has_index_with_AggregateType_and_AggregateId()
        {
            var context = new EventStoreDbContext(_dbContextOptions);
            IEntityType sut = context.Model.FindEntityType(typeof(Aggregate));

            IIndex actual = sut.FindIndex(new[]
            {
                sut.FindProperty("AggregateType"),
                sut.FindProperty("AggregateId"),
            });

            actual.Should().NotBeNull();
            actual.IsUnique.Should().BeTrue();
        }

        [TestMethod]
        public void model_has_PersistentEvent_entity()
        {
            var sut = new EventStoreDbContext(_dbContextOptions);
            IEntityType actual = sut.Model.FindEntityType(typeof(PersistentEvent));
            actual.Should().NotBeNull();
        }

        [TestMethod]
        public void PersistentEvent_entity_has_index_with_AggregateType_AggregateId_and_Version()
        {
            var context = new EventStoreDbContext(_dbContextOptions);
            IEntityType sut = context.Model.FindEntityType(typeof(PersistentEvent));

            IIndex actual = sut.FindIndex(new[]
            {
                sut.FindProperty("AggregateType"),
                sut.FindProperty("AggregateId"),
                sut.FindProperty("Version"),
            });

            actual.Should().NotBeNull();
            actual.IsUnique.Should().BeTrue();
        }

        [TestMethod]
        public void model_has_PendingEvent_entity()
        {
            var sut = new EventStoreDbContext(_dbContextOptions);
            IEntityType actual = sut.Model.FindEntityType(typeof(PendingEvent));
            actual.Should().NotBeNull();
        }

        [TestMethod]
        public void PendingEvent_entity_has_primary_key_with_AggregateId_Version()
        {
            var context = new EventStoreDbContext(_dbContextOptions);
            IEntityType sut = context.Model.FindEntityType(typeof(PendingEvent));
            IKey actual = sut.FindPrimaryKey();
            actual.Should().NotBeNull();
            actual.Properties.Should().Equal(new[]
            {
                sut.FindProperty("AggregateId"),
                sut.FindProperty("Version"),
            });
        }

        [TestMethod]
        public void model_has_UniqueIndexedProperty_entity()
        {
            var sut = new EventStoreDbContext(_dbContextOptions);
            IEntityType actual = sut.Model.FindEntityType(typeof(UniqueIndexedProperty));
            actual.Should().NotBeNull();
        }

        [TestMethod]
        public void UniqueIndexedProperty_entity_has_primary_key_with_AggregateType_PropertyName_PropertyValue()
        {
            var context = new EventStoreDbContext(_dbContextOptions);
            IEntityType sut = context.Model.FindEntityType(typeof(UniqueIndexedProperty));
            IKey actual = sut.FindPrimaryKey();
            actual.Should().NotBeNull();
            actual.Properties.Should().Equal(new[]
            {
                sut.FindProperty("AggregateType"),
                sut.FindProperty("PropertyName"),
                sut.FindProperty("PropertyValue"),
            });
        }

        [TestMethod]
        public void UniqueIndexedProperty_entity_has_index_with_AggregateId_PropertyName()
        {
            var context = new EventStoreDbContext(_dbContextOptions);
            IEntityType sut = context.Model.FindEntityType(typeof(UniqueIndexedProperty));
            IIndex actual = sut.FindIndex(new[]
            {
                sut.FindProperty("AggregateId"),
                sut.FindProperty("PropertyName"),
            });
            actual.Should().NotBeNull();
            actual.IsUnique.Should().BeTrue();
        }

        [TestMethod]
        public void model_has_Correlation_entity()
        {
            var sut = new EventStoreDbContext(_dbContextOptions);
            IEntityType actual = sut.Model.FindEntityType(typeof(Correlation));
            actual.Should().NotBeNull();
        }

        [TestMethod]
        public void Correlation_entity_has_primary_key_with_AggregateId_CorrelationId()
        {
            var context = new EventStoreDbContext(_dbContextOptions);
            IEntityType sut = context.Model.FindEntityType(typeof(Correlation));
            IKey actual = sut.FindPrimaryKey();
            actual.Should().NotBeNull();
            actual.Properties.Should().Equal(new[]
            {
                sut.FindProperty("AggregateId"),
                sut.FindProperty("CorrelationId"),
            });
        }
    }
}
