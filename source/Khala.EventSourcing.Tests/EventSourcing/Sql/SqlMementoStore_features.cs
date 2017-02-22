using System;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Khala.FakeDomain;
using Khala.Messaging;
using Ploeh.AutoFixture;
using Ploeh.AutoFixture.AutoMoq;
using Ploeh.AutoFixture.Idioms;
using Ploeh.AutoFixture.Xunit2;
using Xunit;
using Xunit.Abstractions;

namespace Khala.EventSourcing.Sql
{
    public class SqlMementoStore_features
    {
        private ITestOutputHelper output;
        private IFixture fixture;
        private IMessageSerializer serializer;
        private SqlMementoStore sut;

        public class DataContext : MementoStoreDbContext
        {
        }

        public SqlMementoStore_features(ITestOutputHelper output)
        {
            this.output = output;

            fixture = new Fixture().Customize(new AutoMoqCustomization());
            fixture.Inject<Func<IMementoStoreDbContext>>(() => new DataContext());

            serializer = new JsonMessageSerializer();
            fixture.Inject(serializer);

            sut = new SqlMementoStore(() => new DataContext(), serializer);

            using (var db = new DataContext())
            {
                db.Database.Log = output.WriteLine;
                db.Database.ExecuteSqlCommand("DELETE FROM Mementoes");
            }
        }

        [Fact]
        public void SqlMementoStore_has_guard_clauses()
        {
            var assertion = new GuardClauseAssertion(fixture);
            assertion.Verify(typeof(SqlMementoStore));
        }

        [Theory]
        [AutoData]
        public async Task Save_inserts_Memento_entity_correctly(
            Guid sourceId,
            FakeUserMemento memento)
        {
            await sut.Save<FakeUser>(sourceId, memento, CancellationToken.None);

            using (var db = new DataContext())
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

        [Theory]
        [AutoData]
        public async Task Save_updates_Memento_entity_if_already_exists(
            Guid sourceId,
            FakeUserMemento oldMemento,
            FakeUserMemento newMemento)
        {
            long sequence = 0;
            using (var db = new DataContext())
            {
                var memento = new Memento
                {
                    AggregateId = sourceId,
                    MementoJson = serializer.Serialize(oldMemento)
                };
                db.Mementoes.Add(memento);
                await db.SaveChangesAsync();
                sequence = memento.SequenceId;
            }

            await sut.Save<FakeUser>(sourceId, newMemento, CancellationToken.None);

            using (var db = new DataContext())
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

        [Theory]
        [AutoData]
        public async Task Find_returns_memento_correctly(
            Guid sourceId,
            FakeUserMemento memento)
        {
            await sut.Save<FakeUser>(sourceId, memento, CancellationToken.None);

            IMemento actual = await
                sut.Find<FakeUser>(sourceId, CancellationToken.None);

            actual.Should().BeOfType<FakeUserMemento>();
            actual.ShouldBeEquivalentTo(memento);
        }

        [Theory]
        [AutoData]
        public async Task Find_returns_null_if_not_exists(Guid sourceId)
        {
            IMemento actual = await
                sut.Find<FakeUser>(sourceId, CancellationToken.None);

            actual.Should().BeNull();
        }
    }
}
