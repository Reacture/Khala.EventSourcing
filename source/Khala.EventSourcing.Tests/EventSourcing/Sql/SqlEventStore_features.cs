namespace Khala.EventSourcing.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Khala.FakeDomain;
    using Khala.FakeDomain.Events;
    using Khala.Messaging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Ploeh.AutoFixture;
    using Ploeh.AutoFixture.AutoMoq;
    using Ploeh.AutoFixture.Idioms;

    [TestClass]
    public class SqlEventStore_features
    {
        public class DataContext : EventStoreDbContext
        {
        }

        private IFixture fixture;
        private Guid userId;
        private IMessageSerializer serializer;
        private SqlEventStore sut;
        private EventStoreDbContext mockDbContext;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
        {
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

            using (var db = new DataContext())
            {
                db.Database.Log = m => TestContext?.WriteLine(m);
                db.Database.ExecuteSqlCommand("DELETE FROM Aggregates");
                db.Database.ExecuteSqlCommand("DELETE FROM PersistentEvents");
                db.Database.ExecuteSqlCommand("DELETE FROM PendingEvents");
                db.Database.ExecuteSqlCommand("DELETE FROM UniqueIndexedProperties");
            }
        }

        [TestMethod]
        public void SqlEventStore_has_guard_clauses()
        {
            var assertion = new GuardClauseAssertion(fixture);
            assertion.Verify(typeof(SqlEventStore));
        }

        [TestMethod]
        public void sut_implements_ISqlEventStore()
        {
            sut.Should().BeAssignableTo<ISqlEventStore>();
        }

        [TestMethod]
        public async Task SaveEvents_commits_once()
        {
            var events = new DomainEvent[]
            {
                fixture.Create<FakeUserCreated>(),
                fixture.Create<FakeUsernameChanged>()
            };
            RaiseEvents(userId, events);
            var sut = new SqlEventStore(
                () => mockDbContext,
                new JsonMessageSerializer());

            await sut.SaveEvents<FakeUser>(events);

            Mock.Get(mockDbContext).Verify(
                x => x.SaveChangesAsync(CancellationToken.None), Times.Once());
        }

        [TestMethod]
        public async Task SaveEvents_does_not_commit_for_empty_events()
        {
            var sut = new SqlEventStore(
                () => mockDbContext,
                new JsonMessageSerializer());

            await sut.SaveEvents<FakeUser>(Enumerable.Empty<IDomainEvent>());

            Mock.Get(mockDbContext)
                .Verify(x => x.SaveChangesAsync(), Times.Never());
        }

        [TestMethod]
        public void SaveEvents_fails_if_events_contains_null()
        {
            FakeUserCreated created = fixture.Create<FakeUserCreated>();
            var events = new DomainEvent[] { created, null };
            RaiseEvents(userId, created);

            Func<Task> action = () => sut.SaveEvents<FakeUser>(events);

            action.ShouldThrow<ArgumentException>()
                .Where(x => x.ParamName == "events");
        }

        [TestMethod]
        public async Task SaveEvents_inserts_Aggregate_correctly_for_new_aggregate_id()
        {
            FakeUserCreated created = fixture.Create<FakeUserCreated>();
            var events = new DomainEvent[] { created };
            RaiseEvents(userId, events);

            await sut.SaveEvents<FakeUser>(events);

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

        [TestMethod]
        public async Task SaveEvents_updates_Aggregate_correctly_for_existing_aggregate_id()
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

            var usernameChanged = fixture.Create<FakeUsernameChanged>();
            var events = new DomainEvent[] { usernameChanged };
            RaiseEvents(userId, 1, usernameChanged);

            // Act
            await sut.SaveEvents<FakeUser>(events);

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

        [TestMethod]
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

        [TestMethod]
        public async Task SaveEvents_fails_if_version_of_first_event_not_follows_aggregate()
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

            var usernameChanged = fixture.Create<FakeUsernameChanged>();
            var events = new DomainEvent[] { usernameChanged };
            RaiseEvents(userId, 2, usernameChanged);

            // Act
            Func<Task> action = () => sut.SaveEvents<FakeUser>(events);

            // Assert
            action.ShouldThrow<ArgumentException>()
                .Where(x => x.ParamName == "events");
        }

        [TestMethod]
        public void SaveEvents_fails_if_events_not_have_same_source_id()
        {
            // Arrange
            var created = fixture.Create<FakeUserCreated>();
            created.SourceId = userId;
            created.Version = 1;
            created.RaisedAt = DateTimeOffset.Now;

            var usernameChanged = fixture.Create<FakeUsernameChanged>();
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

        [TestMethod]
        public async Task SaveEvents_saves_pending_events_correctly()
        {
            // Arrange
            var created = fixture.Create<FakeUserCreated>();
            var usernameChanged = fixture.Create<FakeUsernameChanged>();
            var events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(userId, events);
            var correlationId = Guid.NewGuid();

            // Act
            await sut.SaveEvents<FakeUser>(events, correlationId);

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
                        t.Pending.CorrelationId,
                        Message = serializer.Deserialize(t.Pending.EventJson)
                    };
                    actual.ShouldBeEquivalentTo(new
                    {
                        t.Source.Version,
                        CorrelationId = correlationId,
                        Message = t.Source
                    },
                    opts => opts.RespectingRuntimeTypes());
                }
            }
        }

        [TestMethod]
        public async Task SaveEvents_saves_events_correctly()
        {
            // Arrange
            var created = fixture.Create<FakeUserCreated>();
            var usernameChanged = fixture.Create<FakeUsernameChanged>();
            var events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(userId, events);

            // Act
            await sut.SaveEvents<FakeUser>(events);

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

        [TestMethod]
        public async Task SaveEvents_sets_message_properties_correctly()
        {
            // Arrange
            var created = fixture.Create<FakeUserCreated>();
            var usernameChanged = fixture.Create<FakeUsernameChanged>();
            var events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(userId, events);

            // Act
            await sut.SaveEvents<FakeUser>(events, Guid.NewGuid());

            // Asseert
            using (var db = new DataContext())
            {
                IEnumerable<object> expected = db
                    .PendingEvents
                    .Where(e => e.AggregateId == userId)
                    .OrderBy(e => e.Version)
                    .AsEnumerable()
                    .Select(e => new { e.MessageId, e.CorrelationId })
                    .ToList();

                IEnumerable<object> actual = db
                    .PersistentEvents
                    .Where(e => e.AggregateId == userId)
                    .OrderBy(e => e.Version)
                    .AsEnumerable()
                    .Select(e => new { e.MessageId, e.CorrelationId })
                    .ToList();

                actual.ShouldAllBeEquivalentTo(expected);
            }
        }

        [TestMethod]
        public async Task SaveEvents_inserts_UniqueIndexedProperty_for_new_property()
        {
            var created = fixture.Create<FakeUserCreated>();
            var events = new DomainEvent[] { created };
            RaiseEvents(userId, events);

            await sut.SaveEvents<FakeUser>(events);

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

        [TestMethod]
        public async Task SaveEvents_inserts_UniqueIndexedProperty_with_value_of_latest_indexed_event()
        {
            var created = fixture.Create<FakeUserCreated>();
            var usernameChanged = fixture.Create<FakeUsernameChanged>();
            var events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(userId, events);

            await sut.SaveEvents<FakeUser>(events);

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

        [TestMethod]
        public async Task SaveEvents_does_not_insert_UniqueIndexedProperty_if_property_value_is_null()
        {
            var created = new FakeUserCreated { Username = null };
            var events = new DomainEvent[] { created };
            RaiseEvents(userId, events);

            await sut.SaveEvents<FakeUser>(events);

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

        [TestMethod]
        public async Task SaveEvents_removes_existing_UniqueIndexedProperty_if_property_value_is_null()
        {
            // Arrange
            var created = fixture.Create<FakeUserCreated>();
            RaiseEvents(userId, created);
            await sut.SaveEvents<FakeUser>(new[] { created });

            var usernameChanged = fixture.Create<FakeUsernameChanged>();
            usernameChanged.Username = null;
            RaiseEvents(userId, 1, usernameChanged);

            // Act
            await sut.SaveEvents<FakeUser>(new[] { usernameChanged });

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

        [TestMethod]
        public async Task SaveEvents_updates_existing_UniqueIndexedProperty_correctly()
        {
            // Arrange
            var created = fixture.Create<FakeUserCreated>();
            RaiseEvents(userId, created);
            await sut.SaveEvents<FakeUser>(new[] { created });

            var usernameChanged = fixture.Create<FakeUsernameChanged>();
            RaiseEvents(userId, 1, usernameChanged);

            // Act
            await sut.SaveEvents<FakeUser>(new[] { usernameChanged });

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

        [TestMethod]
        public async Task SaveEvents_fails_if_unique_indexed_property_duplicate()
        {
            // Arrange
            var duplicateUserName = fixture.Create<string>();

            var macId = Guid.NewGuid();
            var macCreated = new FakeUserCreated { Username = duplicateUserName };
            RaiseEvents(macId, macCreated);
            await sut.SaveEvents<FakeUser>(new[] { macCreated });

            // Act
            var toshId = Guid.NewGuid();
            var toshCreated = new FakeUserCreated { Username = duplicateUserName };
            RaiseEvents(toshId, toshCreated);
            Func<Task> action = () => sut.SaveEvents<FakeUser>(new[] { toshCreated });

            // Assert
            action.ShouldThrow<Exception>();
            using (var db = new DataContext())
            {
                IQueryable<PersistentEvent> query =
                    from e in db.PersistentEvents
                    where e.AggregateId == toshId
                    select e;
                (await query.AnyAsync()).Should().BeFalse();
            }
        }

        [TestMethod]
        public async Task SaveEvents_inserts_Correlation_entity_correctly()
        {
            var created = fixture.Create<FakeUserCreated>();
            var correlationId = Guid.NewGuid();
            RaiseEvents(userId, created);
            var now = DateTimeOffset.Now;

            await sut.SaveEvents<FakeUser>(new[] { created }, correlationId);

            using (var db = new DataContext())
            {
                Correlation correlation = await db
                    .Correlations
                    .Where(
                        c =>
                        c.AggregateId == userId &&
                        c.CorrelationId == correlationId)
                    .SingleOrDefaultAsync();
                correlation.Should().NotBeNull();
                correlation.HandledAt.Should().BeCloseTo(now);
            }
        }

        [TestMethod]
        public async Task SaveEvents_throws_DuplicateCorrelationException_if_correlation_duplicate()
        {
            var created = fixture.Create<FakeUserCreated>();
            var usernameChanged = fixture.Create<FakeUsernameChanged>();
            RaiseEvents(userId, created, usernameChanged);
            var correlationId = Guid.NewGuid();
            await sut.SaveEvents<FakeUser>(new[] { created }, correlationId);

            Func<Task> action = () => sut.SaveEvents<FakeUser>(new[] { usernameChanged }, correlationId);

            action.ShouldThrow<DuplicateCorrelationException>().Where(
                x =>
                x.SourceType == typeof(FakeUser) &&
                x.SourceId == userId &&
                x.CorrelationId == correlationId &&
                x.InnerException is DbUpdateException);
        }

        [TestMethod]
        public async Task LoadEvents_restores_all_events_correctly()
        {
            // Arrange
            var created = fixture.Create<FakeUserCreated>();
            var usernameChanged = fixture.Create<FakeUsernameChanged>();
            var events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(userId, events);
            await sut.SaveEvents<FakeUser>(events);

            // Act
            IEnumerable<IDomainEvent> actual = await sut.LoadEvents<FakeUser>(userId);

            // Assert
            actual.ShouldAllBeEquivalentTo(events);
        }

        [TestMethod]
        public async Task LoadEvents_restores_events_after_specified_version_correctly()
        {
            // Arrange
            var created = fixture.Create<FakeUserCreated>();
            var usernameChanged = fixture.Create<FakeUsernameChanged>();
            var events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(userId, events);
            await sut.SaveEvents<FakeUser>(events);

            // Act
            IEnumerable<IDomainEvent> actual =
                await sut.LoadEvents<FakeUser>(userId, 1);

            // Assert
            actual.ShouldAllBeEquivalentTo(events.Skip(1));
        }

        [TestMethod]
        public async Task FindIdByUniqueIndexedProperty_returns_null_if_property_not_found()
        {
            string value = fixture.Create("username");
            Guid? actual = await
                sut.FindIdByUniqueIndexedProperty<FakeUser>("Username", value);
            actual.Should().NotHaveValue();
        }

        [TestMethod]
        public async Task FindIdByUniqueIndexedProperty_returns_aggregate_id_if_property_found()
        {
            var created = fixture.Create<FakeUserCreated>();
            RaiseEvents(userId, created);
            await sut.SaveEvents<FakeUser>(new[] { created });

            Guid? actual = await
                sut.FindIdByUniqueIndexedProperty<FakeUser>("Username", created.Username);

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
