using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Ploeh.AutoFixture;
using Ploeh.AutoFixture.AutoMoq;
using Ploeh.AutoFixture.Idioms;
using Ploeh.AutoFixture.Xunit2;
using Arcane.FakeDomain;
using Arcane.FakeDomain.Events;
using Arcane.Messaging;
using Xunit;
using Xunit.Abstractions;

namespace Arcane.EventSourcing.Sql
{
    public class SqlEventStore_features : IDisposable
    {
        public class DataContext : EventStoreDbContext
        {
        }

        private ITestOutputHelper output;
        private IFixture fixture;
        private Guid userId;
        private IMessageSerializer serializer;
        private SqlEventStore sut;
        private EventStoreDbContext mockDbContext;

        public SqlEventStore_features(ITestOutputHelper output)
        {
            this.output = output;

            fixture = new Fixture().Customize(new AutoMoqCustomization());
            fixture.Inject<Func<EventStoreDbContext>>(() => new DataContext());

            userId = Guid.NewGuid();

            serializer = new JsonMessageSerializer();
            fixture.Inject(serializer);

            sut = fixture.Create<SqlEventStore>();

            mockDbContext = Mock.Of<EventStoreDbContext>(
                x => x.SaveChangesAsync() == Task.FromResult(default(int)));

            mockDbContext.Aggregates = Mock.Of<DbSet<Aggregate>>();
            Mock.Get(mockDbContext.Aggregates).SetupData();

            mockDbContext.PersistentEvents = Mock.Of<DbSet<PersistentEvent>>();
            Mock.Get(mockDbContext.PersistentEvents).SetupData();

            mockDbContext.PendingEvents = Mock.Of<DbSet<PendingEvent>>();
            Mock.Get(mockDbContext.PendingEvents).SetupData();

            mockDbContext.UniqueIndexedProperties = Mock.Of<DbSet<UniqueIndexedProperty>>();
            Mock.Get(mockDbContext.UniqueIndexedProperties).SetupData();
        }

        public void Dispose()
        {
            using (var db = new DataContext())
            {
                db.Database.Log = output.WriteLine;
                db.Database.ExecuteSqlCommand("DELETE FROM Aggregates");
                db.Database.ExecuteSqlCommand("DELETE FROM PersistentEvents");
                db.Database.ExecuteSqlCommand("DELETE FROM PendingEvents");
                db.Database.ExecuteSqlCommand("DELETE FROM UniqueIndexedProperties");
            }
        }

        [Fact]
        public void SqlEventStore_has_guard_clauses()
        {
            var assertion = new GuardClauseAssertion(fixture);
            assertion.Verify(typeof(SqlEventStore));
        }

        [Fact]
        public void sut_implements_ISqlEventStore()
        {
            sut.Should().BeAssignableTo<ISqlEventStore>();
        }

        [Theory]
        [AutoData]
        public async Task SaveEvents_commits_once(
            FakeUserCreated created,
            FakeUsernameChanged usernameChanged)
        {
            var events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(userId, events);
            var sut = new SqlEventStore(
                () => mockDbContext,
                new JsonMessageSerializer());

            await sut.SaveEvents<FakeUser>(events, null, CancellationToken.None);

            Mock.Get(mockDbContext).Verify(
                x => x.SaveChangesAsync(CancellationToken.None), Times.Once());
        }

        [Theory]
        [AutoData]
        public async Task SaveEvents_does_not_commit_for_empty_events()
        {
            var sut = new SqlEventStore(
                () => mockDbContext,
                new JsonMessageSerializer());

            await sut.SaveEvents<FakeUser>(Enumerable.Empty<IDomainEvent>(), null, CancellationToken.None);

            Mock.Get(mockDbContext)
                .Verify(x => x.SaveChangesAsync(), Times.Never());
        }

        [Theory]
        [AutoData]
        public void SaveEvents_fails_if_events_contains_null(
            FakeUserCreated created)
        {
            var events = new DomainEvent[] { created, null };
            RaiseEvents(userId, created);

            Func<Task> action = () => sut.SaveEvents<FakeUser>(events, null, CancellationToken.None);

            action.ShouldThrow<ArgumentException>()
                .Where(x => x.ParamName == "events");
        }

        [Theory]
        [AutoData]
        public async Task SaveEvents_inserts_Aggregate_correctly_for_new_aggregate_id(
            FakeUserCreated created)
        {
            var events = new DomainEvent[] { created };
            RaiseEvents(userId, events);

            await sut.SaveEvents<FakeUser>(events, null, CancellationToken.None);

            using (var db = new DataContext())
            {
                Aggregate actual = await db
                    .Aggregates
                    .Where(a => a.AggregateId == userId)
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
            using (var db = new DataContext())
            {
                var aggregate = new Aggregate
                {
                    AggregateId = userId,
                    AggregateType = typeof(FakeUser).FullName,
                    Version = 1
                };
                db.Aggregates.Add(aggregate);
                await db.SaveChangesAsync();
            }
            var events = new DomainEvent[] { usernameChanged };
            RaiseEvents(userId, 1, usernameChanged);

            // Act
            await sut.SaveEvents<FakeUser>(events, null, CancellationToken.None);

            // Assert
            using (var db = new DataContext())
            {
                Aggregate actual = await db
                    .Aggregates
                    .Where(a => a.AggregateId == userId)
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

            Func<Task> action = () => sut.SaveEvents<FakeUser>(events, null, CancellationToken.None);

            action.ShouldThrow<ArgumentException>()
                .Where(x => x.ParamName == "events");
        }

        [Theory]
        [AutoData]
        public async Task SaveEvents_fails_if_version_of_first_event_not_follows_aggregate(
            FakeUsernameChanged usernameChanged)
        {
            // Arrange
            using (var db = new DataContext())
            {
                var aggregate = new Aggregate
                {
                    AggregateId = userId,
                    AggregateType = typeof(FakeUser).FullName,
                    Version = 1
                };
                db.Aggregates.Add(aggregate);
                await db.SaveChangesAsync();
            }
            var events = new DomainEvent[] { usernameChanged };
            RaiseEvents(userId, 2, usernameChanged);

            // Act
            Func<Task> action = () => sut.SaveEvents<FakeUser>(events, null, CancellationToken.None);

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
            created.SourceId = userId;
            created.Version = 1;
            created.RaisedAt = DateTimeOffset.Now;

            usernameChanged.SourceId = Guid.NewGuid();
            usernameChanged.Version = 2;
            usernameChanged.RaisedAt = DateTimeOffset.Now;

            var events = new DomainEvent[] { created, usernameChanged };

            // Act
            Func<Task> action = () => sut.SaveEvents<FakeUser>(events, null, CancellationToken.None);

            // Assert
            action.ShouldThrow<ArgumentException>()
                .Where(x => x.ParamName == "events");
        }

        [Theory]
        [AutoData]
        public async Task SaveEvents_saves_pending_events_correctly(
            FakeUserCreated created,
            FakeUsernameChanged usernameChanged,
            Guid correlationId)
        {
            // Arrange
            var events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(userId, events);

            // Act
            await sut.SaveEvents<FakeUser>(events, correlationId, CancellationToken.None);

            // Asseert
            using (var db = new DataContext())
            {
                List<PendingEvent> pendingEvents = db
                    .PendingEvents
                    .Where(e => e.AggregateId == userId)
                    .OrderBy(e => e.Version)
                    .ToList();

                foreach (var t in pendingEvents.Zip(events, (pending, source) =>
                                  new { Pending = pending, Source = source }))
                {
                    var actual = new
                    {
                        t.Pending.Version,
                        Envelope = (Envelope)serializer.Deserialize(t.Pending.EnvelopeJson)
                    };
                    actual.ShouldBeEquivalentTo(new
                    {
                        t.Source.Version,
                        Envelope = new Envelope(correlationId, t.Source)
                    },
                    opts => opts.Excluding(x => x.Envelope.MessageId));
                }
            }
        }

        [Theory]
        [AutoData]
        public async Task SaveEvents_saves_events_correctly(
            FakeUserCreated created,
            FakeUsernameChanged usernameChanged)
        {
            // Arrange
            var events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(userId, events);

            // Act
            await sut.SaveEvents<FakeUser>(events, null, CancellationToken.None);

            // Asseert
            using (var db = new DataContext())
            {
                IEnumerable<object> actual = db
                    .PersistentEvents
                    .Where(e => e.AggregateId == userId)
                    .OrderBy(e => e.Version)
                    .AsEnumerable()
                    .Select(e => new
                    {
                        e.Version,
                        e.EventType,
                        Payload = serializer.Deserialize(e.EventJson)
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
        public async Task SaveEvents_sets_message_properties_correctly(
            FakeUserCreated created,
            FakeUsernameChanged usernameChanged,
            Guid correlationId)
        {
            // Arrange
            var events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(userId, events);

            // Act
            await sut.SaveEvents<FakeUser>(events, correlationId, CancellationToken.None);

            // Asseert
            using (var db = new DataContext())
            {
                List<Envelope> envelopes = db
                    .PendingEvents
                    .Where(e => e.AggregateId == userId)
                    .OrderBy(e => e.Version)
                    .AsEnumerable()
                    .Select(e => e.EnvelopeJson)
                    .Select(serializer.Deserialize)
                    .Cast<Envelope>()
                    .ToList();

                IEnumerable<object> actual = db
                    .PersistentEvents
                    .Where(e => e.AggregateId == userId)
                    .OrderBy(e => e.Version)
                    .AsEnumerable()
                    .Select(e => new
                    {
                        e.MessageId,
                        e.CorrelationId
                    })
                    .ToList();

                actual.ShouldAllBeEquivalentTo(envelopes.Select(e => new
                {
                    e.MessageId,
                    e.CorrelationId
                }));
            }
        }

        [Theory]
        [AutoData]
        public async Task SaveEvents_inserts_UniqueIndexedProperty_for_new_property(
            FakeUserCreated created)
        {
            var events = new DomainEvent[] { created };
            RaiseEvents(userId, events);

            await sut.SaveEvents<FakeUser>(events, null, CancellationToken.None);

            using (var db = new DataContext())
            {
                UniqueIndexedProperty actual = await db
                    .UniqueIndexedProperties
                    .Where(
                        p =>
                        p.AggregateType == typeof(FakeUser).FullName &&
                        p.PropertyName == nameof(FakeUserCreated.Username) &&
                        p.PropertyValue == created.Username)
                    .SingleOrDefaultAsync();
                actual.Should().NotBeNull();
                actual.AggregateId.Should().Be(userId);
                actual.Version.Should().Be(created.Version);
            }
        }

        [Theory]
        [AutoData]
        public async Task SaveEvents_inserts_UniqueIndexedProperty_with_value_of_latest_indexed_event(
            FakeUserCreated created,
            FakeUsernameChanged usernameChanged)
        {
            var events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(userId, events);

            await sut.SaveEvents<FakeUser>(events, null, CancellationToken.None);

            using (var db = new DataContext())
            {
                UniqueIndexedProperty actual = await db
                    .UniqueIndexedProperties
                    .Where(
                        p =>
                        p.AggregateId == userId &&
                        p.PropertyName == nameof(FakeUserCreated.Username))
                    .SingleOrDefaultAsync();
                actual.PropertyValue.Should().Be(usernameChanged.Username);
                actual.Version.Should().Be(usernameChanged.Version);
            }
        }

        [Fact]
        public async Task SaveEvents_does_not_insert_UniqueIndexedProperty_if_property_value_is_null()
        {
            var created = new FakeUserCreated { Username = null };
            var events = new DomainEvent[] { created };
            RaiseEvents(userId, events);

            await sut.SaveEvents<FakeUser>(events, null, CancellationToken.None);

            using (var db = new DataContext())
            {
                UniqueIndexedProperty actual = await db
                    .UniqueIndexedProperties
                    .Where(
                        p =>
                        p.AggregateType == typeof(FakeUser).FullName &&
                        p.PropertyName == nameof(FakeUserCreated.Username) &&
                        p.PropertyValue == created.Username)
                    .SingleOrDefaultAsync();
                actual.Should().BeNull();
            }
        }

        [Theory]
        [AutoData]
        public async Task SaveEvents_removes_existing_UniqueIndexedProperty_if_property_value_is_null(
            FakeUserCreated created,
            FakeUsernameChanged usernameChanged)
        {
            // Arrange
            RaiseEvents(userId, created);
            await sut.SaveEvents<FakeUser>(new[] { created }, null, CancellationToken.None);
            usernameChanged.Username = null;
            RaiseEvents(userId, 1, usernameChanged);

            // Act
            await sut.SaveEvents<FakeUser>(new[] { usernameChanged }, null, CancellationToken.None);

            // Assert
            using (var db = new DataContext())
            {
                UniqueIndexedProperty actual = await db
                    .UniqueIndexedProperties
                    .Where(
                        p =>
                        p.AggregateType == typeof(FakeUser).FullName &&
                        p.PropertyName == nameof(FakeUserCreated.Username) &&
                        p.PropertyValue == created.Username)
                    .SingleOrDefaultAsync();
                actual.Should().BeNull();
            }
        }

        [Theory]
        [AutoData]
        public async Task SaveEvents_updates_existing_UniqueIndexedProperty_correctly(
            FakeUserCreated created,
            FakeUsernameChanged usernameChanged)
        {
            // Arrange
            RaiseEvents(userId, created);
            await sut.SaveEvents<FakeUser>(new[] { created }, null, CancellationToken.None);
            RaiseEvents(userId, 1, usernameChanged);

            // Act
            await sut.SaveEvents<FakeUser>(new[] { usernameChanged }, null, CancellationToken.None);

            // Assert
            using (var db = new DataContext())
            {
                UniqueIndexedProperty actual = await db
                    .UniqueIndexedProperties
                    .Where(
                        p =>
                        p.AggregateId == userId &&
                        p.PropertyName == nameof(FakeUserCreated.Username))
                    .SingleOrDefaultAsync();
                actual.Should().NotBeNull();
                actual.PropertyValue.Should().Be(usernameChanged.Username);
                actual.Version.Should().Be(usernameChanged.Version);
            }
        }

        [Theory]
        [AutoData]
        public async Task SaveEvents_fails_if_unique_indexed_property_duplicate(
            Guid macId,
            Guid toshId,
            string duplicateUserName)
        {
            // Arrange
            var macCreated = new FakeUserCreated { Username = duplicateUserName };
            RaiseEvents(macId, macCreated);
            await sut.SaveEvents<FakeUser>(new[] { macCreated }, null, CancellationToken.None);

            // Act
            var toshCreated = new FakeUserCreated { Username = duplicateUserName };
            RaiseEvents(toshId, toshCreated);
            Func<Task> action = () => sut.SaveEvents<FakeUser>(new[] { toshCreated }, null, CancellationToken.None);

            // Assert
            action.ShouldThrow<Exception>();
            using (var db = new DataContext())
            {
                IQueryable<PersistentEvent> query = from e in db.PersistentEvents
                                          where e.AggregateId == toshId
                                          select e;
                (await query.AnyAsync()).Should().BeFalse();
            }
        }

        [Theory]
        [AutoData]
        public async Task LoadEvents_restores_all_events_correctly(
            FakeUserCreated created,
            FakeUsernameChanged usernameChanged)
        {
            // Arrange
            var events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(userId, events);
            await sut.SaveEvents<FakeUser>(events, null, CancellationToken.None);

            // Act
            IEnumerable<IDomainEvent> actual =
                await sut.LoadEvents<FakeUser>(userId, 0, CancellationToken.None);

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
            RaiseEvents(userId, events);
            await sut.SaveEvents<FakeUser>(events, null, CancellationToken.None);

            // Act
            IEnumerable<IDomainEvent> actual =
                await sut.LoadEvents<FakeUser>(userId, 1, CancellationToken.None);

            // Assert
            actual.ShouldAllBeEquivalentTo(events.Skip(1));
        }

        [Theory]
        [AutoData]
        public async Task FindIdByUniqueIndexedProperty_returns_null_if_property_not_found()
        {
            string value = fixture.Create("username");
            Guid? actual = await
                sut.FindIdByUniqueIndexedProperty<FakeUser>("Username", value, CancellationToken.None);
            actual.Should().NotHaveValue();
        }

        [Theory]
        [AutoData]
        public async Task FindIdByUniqueIndexedProperty_returns_aggregate_id_if_property_found(
            FakeUserCreated created)
        {
            RaiseEvents(userId, created);
            await sut.SaveEvents<FakeUser>(new[] { created }, null, CancellationToken.None);

            Guid? actual = await
                sut.FindIdByUniqueIndexedProperty<FakeUser>("Username", created.Username, CancellationToken.None);

            actual.Should().Be(userId);
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
