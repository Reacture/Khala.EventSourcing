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
        public void Aggregate_entity_has_index_with_AggregateId()
        {
            var context = new EventStoreDbContext(_dbContextOptions);
            IEntityType sut = context.Model.FindEntityType(typeof(Aggregate));
            IIndex actual = sut.FindIndex(sut.FindProperty("AggregateId"));
            actual.Should().NotBeNull();
            actual.IsUnique.Should().BeTrue();
        }

        [Fact]
        public void model_has_PersistentEvent_entity()
        {
            var sut = new EventStoreDbContext(_dbContextOptions);
            IEntityType actual = sut.Model.FindEntityType(typeof(PersistentEvent));
            actual.Should().NotBeNull();
        }

        [Fact]
        public void PersistentEvent_entity_has_index_with_AggregateId_Version()
        {
            var context = new EventStoreDbContext(_dbContextOptions);
            IEntityType sut = context.Model.FindEntityType(typeof(PersistentEvent));
            IIndex actual = sut.FindIndex(new[]
            {
                sut.FindProperty("AggregateId"),
                sut.FindProperty("Version")
            });
            actual.Should().NotBeNull();
            actual.IsUnique.Should().BeTrue();
        }

        [Fact]
        public void model_has_PendingEvent_entity()
        {
            var sut = new EventStoreDbContext(_dbContextOptions);
            IEntityType actual = sut.Model.FindEntityType(typeof(PendingEvent));
            actual.Should().NotBeNull();
        }

        [Fact]
        public void PendingEvent_entity_has_primary_key_with_AggregateId_Version()
        {
            var context = new EventStoreDbContext(_dbContextOptions);
            IEntityType sut = context.Model.FindEntityType(typeof(PendingEvent));
            IKey actual = sut.FindPrimaryKey();
            actual.Should().NotBeNull();
            actual.Properties.Should().Equal(new[]
            {
                sut.FindProperty("AggregateId"),
                sut.FindProperty("Version")
            });
        }

        [Fact]
        public void model_has_UniqueIndexedProperty_entity()
        {
            var sut = new EventStoreDbContext(_dbContextOptions);
            IEntityType actual = sut.Model.FindEntityType(typeof(UniqueIndexedProperty));
            actual.Should().NotBeNull();
        }

        [Fact]
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
                sut.FindProperty("PropertyValue")
            });
        }

        [Fact]
        public void UniqueIndexedProperty_entity_has_index_with_AggregateId_PropertyName()
        {
            var context = new EventStoreDbContext(_dbContextOptions);
            IEntityType sut = context.Model.FindEntityType(typeof(UniqueIndexedProperty));
            IIndex actual = sut.FindIndex(new[]
            {
                sut.FindProperty("AggregateId"),
                sut.FindProperty("PropertyName")
            });
            actual.Should().NotBeNull();
            actual.IsUnique.Should().BeTrue();
        }

        [Fact]
        public void model_has_Correlation_entity()
        {
            var sut = new EventStoreDbContext(_dbContextOptions);
            IEntityType actual = sut.Model.FindEntityType(typeof(Correlation));
            actual.Should().NotBeNull();
        }

        [Fact]
        public void Correlation_entity_has_primary_key_with_AggregateId_CorrelationId()
        {
            var context = new EventStoreDbContext(_dbContextOptions);
            IEntityType sut = context.Model.FindEntityType(typeof(Correlation));
            IKey actual = sut.FindPrimaryKey();
            actual.Should().NotBeNull();
            actual.Properties.Should().Equal(new[]
            {
                sut.FindProperty("AggregateId"),
                sut.FindProperty("CorrelationId")
            });
        }
    }
}
