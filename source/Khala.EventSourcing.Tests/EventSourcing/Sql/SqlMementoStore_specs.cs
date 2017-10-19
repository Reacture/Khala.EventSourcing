namespace Khala.EventSourcing.Sql
{
    using System;
    using System.Data.Entity;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Khala.FakeDomain;
    using Khala.Messaging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Ploeh.AutoFixture;
    using Ploeh.AutoFixture.AutoMoq;
    using Ploeh.AutoFixture.Idioms;

    [TestClass]
    public class SqlMementoStore_specs
    {
        private IFixture _fixture;
        private IMessageSerializer _serializer;
        private SqlMementoStore _sut;

        private class DataContext : MementoStoreDbContext
        {
        }

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
        {
            _fixture = new Fixture().Customize(new AutoMoqCustomization());
            _fixture.Inject<Func<MementoStoreDbContext>>(() => new DataContext());

            _serializer = new JsonMessageSerializer();
            _fixture.Inject(_serializer);

            _sut = new SqlMementoStore(() => new DataContext(), _serializer);

            using (var db = new DataContext())
            {
                db.Database.Log = m => TestContext?.WriteLine(m);
                db.Database.ExecuteSqlCommand("DELETE FROM Mementoes");
            }
        }

        [TestMethod]
        public void SqlMementoStore_has_guard_clauses()
        {
            var assertion = new GuardClauseAssertion(_fixture);
            assertion.Verify(typeof(SqlMementoStore));
        }

        [TestMethod]
        public async Task Save_inserts_Memento_entity_correctly()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var memento = _fixture.Create<FakeUserMemento>();

            // Act
            await _sut.Save<FakeUser>(sourceId, memento, CancellationToken.None);

            // Assert
            using (var db = new DataContext())
            {
                Memento actual = await db
                    .Mementoes
                    .AsNoTracking()
                    .Where(m => m.AggregateId == sourceId)
                    .SingleOrDefaultAsync();

                actual.Should().NotBeNull();
                object restored = _serializer.Deserialize(actual.MementoJson);
                restored.Should().BeOfType<FakeUserMemento>();
                restored.ShouldBeEquivalentTo(memento);
            }
        }

        [TestMethod]
        public async Task Save_updates_Memento_entity_if_already_exists()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var oldMemento = _fixture.Create<FakeUserMemento>();
            var newMemento = _fixture.Create<FakeUserMemento>();

            long sequence = 0;
            using (var db = new DataContext())
            {
                var memento = new Memento
                {
                    AggregateId = sourceId,
                    MementoJson = _serializer.Serialize(oldMemento)
                };
                db.Mementoes.Add(memento);
                await db.SaveChangesAsync();
                sequence = memento.SequenceId;
            }

            // Act
            await _sut.Save<FakeUser>(sourceId, newMemento, CancellationToken.None);

            // Assert
            using (var db = new DataContext())
            {
                Memento actual = await db
                    .Mementoes
                    .Where(m => m.SequenceId == sequence)
                    .SingleOrDefaultAsync();

                actual.Should().NotBeNull();
                actual.AggregateId.Should().Be(sourceId);
                object restored = _serializer.Deserialize(actual.MementoJson);
                restored.Should().BeOfType<FakeUserMemento>();
                restored.ShouldBeEquivalentTo(newMemento);
            }
        }

        [TestMethod]
        public async Task Find_returns_memento_correctly()
        {
            var sourceId = Guid.NewGuid();
            var memento = _fixture.Create<FakeUserMemento>();
            await _sut.Save<FakeUser>(sourceId, memento, CancellationToken.None);

            IMemento actual = await
                _sut.Find<FakeUser>(sourceId, CancellationToken.None);

            actual.Should().BeOfType<FakeUserMemento>();
            actual.ShouldBeEquivalentTo(memento);
        }

        [TestMethod]
        public async Task Find_returns_null_if_not_exists()
        {
            var sourceId = Guid.NewGuid();

            IMemento actual = await
                _sut.Find<FakeUser>(sourceId, CancellationToken.None);

            actual.Should().BeNull();
        }

        [TestMethod]
        public async Task Delete_deletes_Memento_entity()
        {
            var sourceId = Guid.NewGuid();
            var memento = _fixture.Create<FakeUserMemento>();
            await _sut.Save<FakeUser>(sourceId, memento, CancellationToken.None);

            await _sut.Delete<FakeUser>(sourceId, CancellationToken.None);

            using (var db = new DataContext())
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
            var sourceId = Guid.NewGuid();
            Func<Task> action = () => _sut.Delete<FakeUser>(sourceId, CancellationToken.None);
            action.ShouldNotThrow();
        }
    }
}
