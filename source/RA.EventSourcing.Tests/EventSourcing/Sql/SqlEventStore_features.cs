using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Ploeh.AutoFixture;
using Ploeh.AutoFixture.AutoMoq;
using Ploeh.AutoFixture.Idioms;
using Ploeh.AutoFixture.Xunit2;
using ReactiveArchitecture.FakeDomain;
using ReactiveArchitecture.FakeDomain.Events;
using ReactiveArchitecture.Messaging;
using Xunit;
using Xunit.Abstractions;

namespace ReactiveArchitecture.EventSourcing.Sql
{
    public class SqlEventStore_features : IDisposable
    {
        private ITestOutputHelper output;
        private IFixture fixture;
        private Guid aggregateId;
        private SqlEventStore sut;
        private EventStoreDbContext mockDbContext;

        public SqlEventStore_features(ITestOutputHelper output)
        {
            this.output = output;

            fixture = new Fixture().Customize(new AutoMoqCustomization());
            fixture.Inject<Func<EventStoreDbContext>>(() => new EventStoreDbContext());

            aggregateId = Guid.NewGuid();

            sut = fixture.Create<SqlEventStore>();

            mockDbContext = Mock.Of<EventStoreDbContext>(
                x => x.SaveChangesAsync() == Task.FromResult(default(int)));

            mockDbContext.Aggregates = Mock.Of<DbSet<Aggregate>>();
            Mock.Get(mockDbContext.Aggregates).SetupData();

            mockDbContext.Events = Mock.Of<DbSet<Event>>();
            Mock.Get(mockDbContext.Events).SetupData();

            mockDbContext.PendingEvents = Mock.Of<DbSet<PendingEvent>>();
            Mock.Get(mockDbContext.PendingEvents).SetupData();
        }

        public void Dispose()
        {
            using (var db = new EventStoreDbContext())
            {
                db.Database.Log = output.WriteLine;
                db.Database.ExecuteSqlCommand("DELETE FROM Aggregates WHERE AggregateId = @p0", aggregateId);
                db.Database.ExecuteSqlCommand("DELETE FROM Events WHERE AggregateId = @p0", aggregateId);
                db.Database.ExecuteSqlCommand("DELETE FROM PendingEvents WHERE AggregateId = @p0", aggregateId);
            }
        }

        [Fact]
        public void SqlEventStore_has_guard_clauses()
        {
            var assertion = new GuardClauseAssertion(fixture);
            assertion.Verify(typeof(SqlEventStore));
        }

        [Fact]
        public void sut_implements_IEventStore()
        {
            sut.Should().BeAssignableTo<IEventStore>();
        }

        [Theory]
        [AutoData]
        public void SaveEvents_fails_if_events_contains_null(
            FakeUserCreated created)
        {
            var events = new DomainEvent[] { created, null };
            RaiseEvents(aggregateId, created);

            Func<Task> action = () => sut.SaveEvents<FakeUser>(events);

            action.ShouldThrow<ArgumentException>()
                .Where(x => x.ParamName == "events");
        }

        [Theory]
        [AutoData]
        public async Task SaveEvents_inserts_Aggregate_correctly_for_new_aggregate_id(
            FakeUserCreated created)
        {
            var events = new DomainEvent[] { created };
            RaiseEvents(aggregateId, created);

            await sut.SaveEvents<FakeUser>(events);

            using (var db = new EventStoreDbContext())
            {
                Aggregate actual = await db
                    .Aggregates
                    .Where(a => a.AggregateId == aggregateId)
                    .SingleOrDefaultAsync();
                actual.Should().NotBeNull();
                actual.AggregateType.Should().Be(typeof(FakeUser).FullName);
                actual.Version.Should().Be(created.Version);
            }
        }

        [Theory]
        [AutoData]
        public async Task SaveEvents_updates_Aggregate_correctly_for_existing_aggregate_id(
            FakeUsernameChanged usernameChanged)
        {
            // Arrange
            using (var db = new EventStoreDbContext())
            {
                var aggregate = new Aggregate
                {
                    AggregateId = aggregateId,
                    AggregateType = typeof(FakeUser).FullName,
                    Version = 1
                };
                db.Aggregates.Add(aggregate);
                await db.SaveChangesAsync();
            }
            var events = new DomainEvent[] { usernameChanged };
            RaiseEvents(aggregateId, 1, usernameChanged);

            // Act
            await sut.SaveEvents<FakeUser>(events);

            // Assert
            using (var db = new EventStoreDbContext())
            {
                Aggregate actual = await db
                    .Aggregates
                    .Where(a => a.AggregateId == aggregateId)
                    .SingleOrDefaultAsync();
                actual.Version.Should().Be(usernameChanged.Version);
            }
        }

        [Fact]
        public void SaveEvents_fails_if_versions_not_sequential()
        {
            var events = new DomainEvent[]
            {
                new FakeUserCreated { Version = 0 },
                new FakeUsernameChanged { Version = 1 },
                new FakeUsernameChanged { Version = 3 }
            };

            Func<Task> action = () => sut.SaveEvents<FakeUser>(events);

            action.ShouldThrow<ArgumentException>()
                .Where(x => x.ParamName == "events");
        }

        [Theory]
        [AutoData]
        public async Task SaveEvents_fails_if_version_of_first_event_not_follows_aggregate(
            FakeUsernameChanged usernameChanged)
        {
            // Arrange
            using (var db = new EventStoreDbContext())
            {
                var aggregate = new Aggregate
                {
                    AggregateId = aggregateId,
                    AggregateType = typeof(FakeUser).FullName,
                    Version = 1
                };
                db.Aggregates.Add(aggregate);
                await db.SaveChangesAsync();
            }
            var events = new DomainEvent[] { usernameChanged };
            RaiseEvents(aggregateId, 2, usernameChanged);

            // Act
            Func<Task> action = () => sut.SaveEvents<FakeUser>(events);

            // Assert
            action.ShouldThrow<ArgumentException>()
                .Where(x => x.ParamName == "events");
        }

        [Theory]
        [AutoData]
        public void SaveEvents_fails_if_events_not_have_same_source_id(
            FakeUserCreated created,
            FakeUsernameChanged usernameChanged)
        {
            // Arrange
            created.SourceId = aggregateId;
            created.Version = 1;
            created.RaisedAt = DateTimeOffset.Now;

            usernameChanged.SourceId = Guid.NewGuid();
            usernameChanged.Version = 2;
            usernameChanged.RaisedAt = DateTimeOffset.Now;

            var events = new DomainEvent[] { created, usernameChanged };

            // Act
            Func<Task> action = () => sut.SaveEvents<FakeUser>(events);

            // Assert
            action.ShouldThrow<ArgumentException>()
                .Where(x => x.ParamName == "events");
        }

        [Theory]
        [AutoData]
        public async Task SaveEvents_saves_events_correctly(
            FakeUserCreated created,
            FakeUsernameChanged usernameChanged)
        {
            // Arrange
            var events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(aggregateId, events);

            // Act
            await sut.SaveEvents<FakeUser>(events);

            // Asseert
            using (var db = new EventStoreDbContext())
            {
                var serializer = new JsonMessageSerializer();

                IEnumerable<object> actual = db
                    .Events
                    .Where(e => e.AggregateId == aggregateId)
                    .OrderBy(e => e.Version)
                    .AsEnumerable()
                    .Select(e => new
                    {
                        e.Version,
                        e.EventType,
                        Payload = serializer.Deserialize(e.PayloadJson)
                    })
                    .ToList();

                actual.Should().HaveCount(events.Length);

                IEnumerable<object> expected = events.Select(e => new
                {
                    Version = e.Version,
                    EventType = e.GetType().FullName,
                    Payload = e
                });

                actual.ShouldAllBeEquivalentTo(expected);
            }
        }

        [Theory]
        [AutoData]
        public async Task SaveEvents_saves_pending_events_correctly(
            FakeUserCreated created,
            FakeUsernameChanged usernameChanged)
        {
            // Arrange
            var events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(aggregateId, events);

            // Act
            await sut.SaveEvents<FakeUser>(events);

            // Asseert
            using (var db = new EventStoreDbContext())
            {
                var serializer = new JsonMessageSerializer();

                IEnumerable<object> actual = db
                    .PendingEvents
                    .Where(e => e.AggregateId == aggregateId)
                    .OrderBy(e => e.Version)
                    .AsEnumerable()
                    .Select(e => new
                    {
                        e.Version,
                        Payload = serializer.Deserialize(e.PayloadJson)
                    })
                    .ToList();

                actual.Should().HaveCount(events.Length);

                IEnumerable<object> expected = events.Select(e => new
                {
                    Version = e.Version,
                    Payload = e
                });

                actual.ShouldAllBeEquivalentTo(expected);
            }
        }

        [Theory]
        [AutoData]
        public async Task SaveEvents_commits_once(
            FakeUserCreated created,
            FakeUsernameChanged usernameChanged)
        {
            var events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(aggregateId, events);
            var sut = new SqlEventStore(
                () => mockDbContext,
                new JsonMessageSerializer());

            await sut.SaveEvents<FakeUser>(events);

            Mock.Get(mockDbContext)
                .Verify(x => x.SaveChangesAsync(), Times.Once());
        }

        [Theory]
        [AutoData]
        public async Task SaveEvents_does_not_commit_for_empty_events()
        {
            var sut = new SqlEventStore(
                () => mockDbContext,
                new JsonMessageSerializer());

            await sut.SaveEvents<FakeUser>(Enumerable.Empty<IDomainEvent>());

            Mock.Get(mockDbContext)
                .Verify(x => x.SaveChangesAsync(), Times.Never());
        }

        [Theory]
        [AutoData]
        public async Task LoadEvents_restores_all_events_correctly(
            FakeUserCreated created,
            FakeUsernameChanged usernameChanged)
        {
            // Arrange
            var events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(aggregateId, events);
            await sut.SaveEvents<FakeUser>(events);

            // Act
            IEnumerable<IDomainEvent> actual =
                await sut.LoadEvents<FakeUser>(aggregateId);

            // Assert
            actual.ShouldAllBeEquivalentTo(events);
        }

        [Theory]
        [AutoData]
        public async Task LoadEvents_restores_events_after_specified_version_correctly(
            FakeUserCreated created,
            FakeUsernameChanged usernameChanged)
        {
            // Arrange
            var events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(aggregateId, events);
            await sut.SaveEvents<FakeUser>(events);

            // Act
            IEnumerable<IDomainEvent> actual =
                await sut.LoadEvents<FakeUser>(aggregateId, afterVersion: 1);

            // Assert
            actual.ShouldAllBeEquivalentTo(events.Skip(1));
        }

        private void RaiseEvents(Guid sourceId, params DomainEvent[] events)
        {
            RaiseEvents(sourceId, 0, events);
        }

        private void RaiseEvents(
            Guid sourceId, int versionOffset, params DomainEvent[] events)
        {
            for (int i = 0; i < events.Length; i++)
            {
                events[i].SourceId = sourceId;
                events[i].Version = versionOffset + i + 1;
                events[i].RaisedAt = DateTimeOffset.Now;
            }
        }
    }
}
