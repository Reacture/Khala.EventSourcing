namespace Khala.EventSourcing.Sql
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Khala.FakeDomain;
    using Khala.Messaging;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SqlMementoStore_specs
    {
        private DbContextOptions _dbContextOptions;

        [TestInitialize]
        public void TestInitialize()
        {
            _dbContextOptions = new DbContextOptionsBuilder()
                .UseInMemoryDatabase(nameof(SqlMementoStore_specs))
                .Options;
        }

        [TestMethod]
        public async Task Save_inserts_Memento_entity_correctly()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var memento = new FakeUserMemento();

            var serializer = new JsonMessageSerializer();

            var sut = new SqlMementoStore(
                () => new MementoStoreDbContext(_dbContextOptions),
                serializer);

            // Act
            await sut.Save<FakeUser>(sourceId, memento, default);

            // Assert
            using (var db = new MementoStoreDbContext(_dbContextOptions))
            {
                Memento actual = await db
                    .Mementoes
                    .AsNoTracking()
                    .Where(m => m.AggregateId == sourceId)
                    .SingleOrDefaultAsync();

                actual.Should().NotBeNull();
                object restored = serializer.Deserialize(actual.MementoJson);
                restored.Should().BeOfType<FakeUserMemento>();
                restored.ShouldBeEquivalentTo(memento);
            }
        }

        [TestMethod]
        public async Task Save_updates_Memento_entity_if_already_exists()
        {
            // Arrange
            var sourceId = Guid.NewGuid();

            var oldMemento = new FakeUserMemento();
            var newMemento = new FakeUserMemento();

            var serializer = new JsonMessageSerializer();

            long sequence = 0;
            using (var db = new MementoStoreDbContext(_dbContextOptions))
            {
                var memento = new Memento
                {
                    AggregateId = sourceId,
                    MementoJson = serializer.Serialize(oldMemento),
                };
                db.Mementoes.Add(memento);
                await db.SaveChangesAsync();
                sequence = memento.SequenceId;
            }

            var sut = new SqlMementoStore(
                () => new MementoStoreDbContext(_dbContextOptions),
                serializer);

            // Act
            await sut.Save<FakeUser>(sourceId, newMemento, default);

            // Assert
            using (var db = new MementoStoreDbContext(_dbContextOptions))
            {
                Memento actual = await db
                    .Mementoes
                    .Where(m => m.SequenceId == sequence)
                    .SingleOrDefaultAsync();

                actual.Should().NotBeNull();
                actual.AggregateId.Should().Be(sourceId);
                object restored = serializer.Deserialize(actual.MementoJson);
                restored.Should().BeOfType<FakeUserMemento>();
                restored.ShouldBeEquivalentTo(newMemento);
            }
        }

        [TestMethod]
        public async Task Find_returns_memento_correctly()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var memento = new FakeUserMemento();
            var sut = new SqlMementoStore(
                () => new MementoStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());
            await sut.Save<FakeUser>(sourceId, memento, default);

            // Act
            IMemento actual = await sut.Find<FakeUser>(sourceId, default);

            // Assert
            actual.Should().BeOfType<FakeUserMemento>();
            actual.ShouldBeEquivalentTo(memento);
        }

        [TestMethod]
        public async Task Find_returns_null_if_not_exists()
        {
            var sourceId = Guid.NewGuid();
            var sut = new SqlMementoStore(
                () => new MementoStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());

            IMemento actual = await sut.Find<FakeUser>(sourceId, default);

            actual.Should().BeNull();
        }

        [TestMethod]
        public async Task Delete_deletes_Memento_entity()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var memento = new FakeUserMemento();

            var sut = new SqlMementoStore(
                () => new MementoStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());

            await sut.Save<FakeUser>(sourceId, memento, default);

            // Act
            await sut.Delete<FakeUser>(sourceId, default);

            // Assert
            using (var db = new MementoStoreDbContext(_dbContextOptions))
            {
                bool actual = await db
                    .Mementoes
                    .Where(m => m.AggregateId == sourceId)
                    .AnyAsync();
                actual.Should().BeFalse();
            }
        }

        [TestMethod]
        public void Delete_does_not_fail_even_if_Memento_entity_does_not_exist()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var sut = new SqlMementoStore(
                () => new MementoStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());

            // Act
            Func<Task> action = () => sut.Delete<FakeUser>(sourceId, default);

            // Assert
            action.ShouldNotThrow();
        }
    }
}
