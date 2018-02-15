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

    [TestClass]
    public class SqlEventStore_specs
    {
        private class DataContext : EventStoreDbContext
        {
        }

        private IFixture _fixture;
        private Guid _userId;
        private IMessageSerializer _serializer;
        private SqlEventStore _sut;
        private EventStoreDbContext _mockDbContext;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
        {
            _fixture = new Fixture().Customize(new AutoMoqCustomization());
            _fixture.Inject<Func<EventStoreDbContext>>(() => new DataContext());

            _userId = Guid.NewGuid();

            _serializer = new JsonMessageSerializer();
            _fixture.Inject(_serializer);

            _sut = _fixture.Create<SqlEventStore>();

            _mockDbContext = Mock.Of<EventStoreDbContext>(
                x => x.SaveChangesAsync() == Task.FromResult(default(int)));

            _mockDbContext.Aggregates = Mock.Of<DbSet<Aggregate>>();
            Mock.Get(_mockDbContext.Aggregates).SetupData();

            _mockDbContext.PersistentEvents = Mock.Of<DbSet<PersistentEvent>>();
            Mock.Get(_mockDbContext.PersistentEvents).SetupData();

            _mockDbContext.PendingEvents = Mock.Of<DbSet<PendingEvent>>();
            Mock.Get(_mockDbContext.PendingEvents).SetupData();

            _mockDbContext.UniqueIndexedProperties = Mock.Of<DbSet<UniqueIndexedProperty>>();
            Mock.Get(_mockDbContext.UniqueIndexedProperties).SetupData();

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
        public void sut_implements_ISqlEventStore()
        {
            _sut.Should().BeAssignableTo<ISqlEventStore>();
        }

        [TestMethod]
        public async Task SaveEvents_commits_once()
        {
            DomainEvent[] events = new DomainEvent[]
            {
                _fixture.Create<FakeUserCreated>(),
                _fixture.Create<FakeUsernameChanged>(),
            };
            RaiseEvents(_userId, events);
            var sut = new SqlEventStore(
                () => _mockDbContext,
                new JsonMessageSerializer());

            await sut.SaveEvents<FakeUser>(events);

            Mock.Get(_mockDbContext).Verify(
                x => x.SaveChangesAsync(CancellationToken.None), Times.Once());
        }

        [TestMethod]
        public async Task SaveEvents_does_not_commit_for_empty_events()
        {
            var sut = new SqlEventStore(
                () => _mockDbContext,
                new JsonMessageSerializer());

            await sut.SaveEvents<FakeUser>(Enumerable.Empty<IDomainEvent>());

            Mock.Get(_mockDbContext)
                .Verify(x => x.SaveChangesAsync(), Times.Never());
        }

        [TestMethod]
        public void SaveEvents_fails_if_events_contains_null()
        {
            FakeUserCreated created = _fixture.Create<FakeUserCreated>();
            DomainEvent[] events = new DomainEvent[] { created, null };
            RaiseEvents(_userId, created);

            Func<Task> action = () => _sut.SaveEvents<FakeUser>(events);

            action.ShouldThrow<ArgumentException>()
                .Where(x => x.ParamName == "events");
        }

        [TestMethod]
        public async Task SaveEvents_inserts_Aggregate_correctly_for_new_aggregate_id()
        {
            FakeUserCreated created = _fixture.Create<FakeUserCreated>();
            DomainEvent[] events = new DomainEvent[] { created };
            RaiseEvents(_userId, events);

            await _sut.SaveEvents<FakeUser>(events);

            using (var db = new DataContext())
            {
                Aggregate actual = await db
                    .Aggregates
                    .Where(a => a.AggregateId == _userId)
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
                    AggregateId = _userId,
                    AggregateType = typeof(FakeUser).FullName,
                    Version = 1,
                };
                db.Aggregates.Add(aggregate);
                await db.SaveChangesAsync();
            }

            FakeUsernameChanged usernameChanged = _fixture.Create<FakeUsernameChanged>();
            DomainEvent[] events = new DomainEvent[] { usernameChanged };
            RaiseEvents(_userId, 1, usernameChanged);

            // Act
            await _sut.SaveEvents<FakeUser>(events);

            // Assert
            using (var db = new DataContext())
            {
                Aggregate actual = await db
                    .Aggregates
                    .Where(a => a.AggregateId == _userId)
                    .SingleOrDefaultAsync();
                actual.Version.Should().Be(usernameChanged.Version);
            }
        }

        [TestMethod]
        public void SaveEvents_fails_if_versions_not_sequential()
        {
            DomainEvent[] events = new DomainEvent[]
            {
                new FakeUserCreated { Version = 0 },
                new FakeUsernameChanged { Version = 1 },
                new FakeUsernameChanged { Version = 3 },
            };

            Func<Task> action = () => _sut.SaveEvents<FakeUser>(events);

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
                    AggregateId = _userId,
                    AggregateType = typeof(FakeUser).FullName,
                    Version = 1,
                };
                db.Aggregates.Add(aggregate);
                await db.SaveChangesAsync();
            }

            FakeUsernameChanged usernameChanged = _fixture.Create<FakeUsernameChanged>();
            DomainEvent[] events = new DomainEvent[] { usernameChanged };
            RaiseEvents(_userId, 2, usernameChanged);

            // Act
            Func<Task> action = () => _sut.SaveEvents<FakeUser>(events);

            // Assert
            action.ShouldThrow<ArgumentException>()
                .Where(x => x.ParamName == "events");
        }

        [TestMethod]
        public void SaveEvents_fails_if_events_not_have_same_source_id()
        {
            // Arrange
            FakeUserCreated created = _fixture.Create<FakeUserCreated>();
            created.SourceId = _userId;
            created.Version = 1;
            created.RaisedAt = DateTime.UtcNow;

            FakeUsernameChanged usernameChanged = _fixture.Create<FakeUsernameChanged>();
            usernameChanged.SourceId = Guid.NewGuid();
            usernameChanged.Version = 2;
            usernameChanged.RaisedAt = DateTime.UtcNow;

            DomainEvent[] events = new DomainEvent[] { created, usernameChanged };

            // Act
            Func<Task> action = () => _sut.SaveEvents<FakeUser>(events);

            // Assert
            action.ShouldThrow<ArgumentException>()
                .Where(x => x.ParamName == "events");
        }

        [TestMethod]
        public async Task SaveEvents_saves_pending_events_correctly()
        {
            // Arrange
            FakeUserCreated created = _fixture.Create<FakeUserCreated>();
            FakeUsernameChanged usernameChanged = _fixture.Create<FakeUsernameChanged>();
            DomainEvent[] events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(_userId, events);
            var operationId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            string contributor = _fixture.Create<string>();

            // Act
            await _sut.SaveEvents<FakeUser>(events, operationId, correlationId, contributor);

            // Asseert
            using (var db = new DataContext())
            {
                var pendingEvents = db
                    .PendingEvents
                    .Where(e => e.AggregateId == _userId)
                    .OrderBy(e => e.Version)
                    .ToList();

                foreach (var t in pendingEvents.Zip(events, (pending, source) =>
                                  new { Pending = pending, Source = source }))
                {
                    var actual = new
                    {
                        t.Pending.Version,
                        t.Pending.OperationId,
                        t.Pending.CorrelationId,
                        t.Pending.Contributor,
                        Message = _serializer.Deserialize(t.Pending.EventJson),
                    };
                    actual.ShouldBeEquivalentTo(new
                    {
                        t.Source.Version,
                        OperationId = operationId,
                        CorrelationId = correlationId,
                        Contributor = contributor,
                        Message = t.Source,
                    },
                    opts => opts.RespectingRuntimeTypes());
                }
            }
        }

        [TestMethod]
        public async Task SaveEvents_saves_events_correctly()
        {
            // Arrange
            FakeUserCreated created = _fixture.Create<FakeUserCreated>();
            FakeUsernameChanged usernameChanged = _fixture.Create<FakeUsernameChanged>();
            DomainEvent[] events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(_userId, events);
            var operationId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            string contributor = _fixture.Create<string>();

            // Act
            await _sut.SaveEvents<FakeUser>(events, operationId, correlationId, contributor);

            // Asseert
            using (var db = new DataContext())
            {
                IEnumerable<object> actual = db
                    .PersistentEvents
                    .Where(e => e.AggregateId == _userId)
                    .OrderBy(e => e.Version)
                    .AsEnumerable()
                    .Select(e => new
                    {
                        e.Version,
                        e.EventType,
                        e.OperationId,
                        e.CorrelationId,
                        e.Contributor,
                        Payload = _serializer.Deserialize(e.EventJson),
                    })
                    .ToList();

                actual.Should().HaveCount(events.Length);

                IEnumerable<object> expected = events.Select(e => new
                {
                    e.Version,
                    EventType = e.GetType().FullName,
                    OperationId = operationId,
                    CorrelationId = correlationId,
                    Contributor = contributor,
                    Payload = e,
                });

                actual.ShouldAllBeEquivalentTo(expected);
            }
        }

        [TestMethod]
        public async Task SaveEvents_inserts_UniqueIndexedProperty_for_new_property()
        {
            FakeUserCreated created = _fixture.Create<FakeUserCreated>();
            DomainEvent[] events = new DomainEvent[] { created };
            RaiseEvents(_userId, events);

            await _sut.SaveEvents<FakeUser>(events);

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
                actual.AggregateId.Should().Be(_userId);
                actual.Version.Should().Be(created.Version);
            }
        }

        [TestMethod]
        public async Task SaveEvents_inserts_UniqueIndexedProperty_with_value_of_latest_indexed_event()
        {
            FakeUserCreated created = _fixture.Create<FakeUserCreated>();
            FakeUsernameChanged usernameChanged = _fixture.Create<FakeUsernameChanged>();
            DomainEvent[] events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(_userId, events);

            await _sut.SaveEvents<FakeUser>(events);

            using (var db = new DataContext())
            {
                UniqueIndexedProperty actual = await db
                    .UniqueIndexedProperties
                    .Where(
                        p =>
                        p.AggregateId == _userId &&
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
            DomainEvent[] events = new DomainEvent[] { created };
            RaiseEvents(_userId, events);

            await _sut.SaveEvents<FakeUser>(events);

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
            FakeUserCreated created = _fixture.Create<FakeUserCreated>();
            RaiseEvents(_userId, created);
            await _sut.SaveEvents<FakeUser>(new[] { created });

            FakeUsernameChanged usernameChanged = _fixture.Create<FakeUsernameChanged>();
            usernameChanged.Username = null;
            RaiseEvents(_userId, 1, usernameChanged);

            // Act
            await _sut.SaveEvents<FakeUser>(new[] { usernameChanged });

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
            FakeUserCreated created = _fixture.Create<FakeUserCreated>();
            RaiseEvents(_userId, created);
            await _sut.SaveEvents<FakeUser>(new[] { created });

            FakeUsernameChanged usernameChanged = _fixture.Create<FakeUsernameChanged>();
            RaiseEvents(_userId, 1, usernameChanged);

            // Act
            await _sut.SaveEvents<FakeUser>(new[] { usernameChanged });

            // Assert
            using (var db = new DataContext())
            {
                UniqueIndexedProperty actual = await db
                    .UniqueIndexedProperties
                    .Where(
                        p =>
                        p.AggregateId == _userId &&
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
            string duplicateUserName = _fixture.Create<string>();

            var macId = Guid.NewGuid();
            var macCreated = new FakeUserCreated { Username = duplicateUserName };
            RaiseEvents(macId, macCreated);
            await _sut.SaveEvents<FakeUser>(new[] { macCreated });

            // Act
            var toshId = Guid.NewGuid();
            var toshCreated = new FakeUserCreated { Username = duplicateUserName };
            RaiseEvents(toshId, toshCreated);
            Func<Task> action = () => _sut.SaveEvents<FakeUser>(new[] { toshCreated });

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
            FakeUserCreated created = _fixture.Create<FakeUserCreated>();
            var correlationId = Guid.NewGuid();
            RaiseEvents(_userId, created);
            DateTime now = DateTime.UtcNow;

            await _sut.SaveEvents<FakeUser>(new[] { created }, correlationId: correlationId);

            using (var db = new DataContext())
            {
                Correlation correlation = await db
                    .Correlations
                    .Where(
                        c =>
                        c.AggregateId == _userId &&
                        c.CorrelationId == correlationId)
                    .SingleOrDefaultAsync();
                correlation.Should().NotBeNull();
                correlation.HandledAt.Should().BeCloseTo(now);
            }
        }

        [TestMethod]
        public async Task SaveEvents_throws_DuplicateCorrelationException_if_correlation_duplicate()
        {
            FakeUserCreated created = _fixture.Create<FakeUserCreated>();
            FakeUsernameChanged usernameChanged = _fixture.Create<FakeUsernameChanged>();
            RaiseEvents(_userId, created, usernameChanged);
            var correlationId = Guid.NewGuid();
            await _sut.SaveEvents<FakeUser>(new[] { created }, correlationId: correlationId);

            Func<Task> action = () =>
            _sut.SaveEvents<FakeUser>(new[] { usernameChanged }, correlationId: correlationId);

            action.ShouldThrow<DuplicateCorrelationException>().Where(
                x =>
                x.SourceType == typeof(FakeUser) &&
                x.SourceId == _userId &&
                x.CorrelationId == correlationId &&
                x.InnerException is DbUpdateException);
        }

        [TestMethod]
        public async Task LoadEvents_restores_all_events_correctly()
        {
            // Arrange
            FakeUserCreated created = _fixture.Create<FakeUserCreated>();
            FakeUsernameChanged usernameChanged = _fixture.Create<FakeUsernameChanged>();
            DomainEvent[] events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(_userId, events);
            await _sut.SaveEvents<FakeUser>(events);

            // Act
            IEnumerable<IDomainEvent> actual = await _sut.LoadEvents<FakeUser>(_userId);

            // Assert
            actual.ShouldAllBeEquivalentTo(events);
        }

        [TestMethod]
        public async Task LoadEvents_restores_events_after_specified_version_correctly()
        {
            // Arrange
            FakeUserCreated created = _fixture.Create<FakeUserCreated>();
            FakeUsernameChanged usernameChanged = _fixture.Create<FakeUsernameChanged>();
            DomainEvent[] events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(_userId, events);
            await _sut.SaveEvents<FakeUser>(events);

            // Act
            IEnumerable<IDomainEvent> actual =
                await _sut.LoadEvents<FakeUser>(_userId, 1);

            // Assert
            actual.ShouldAllBeEquivalentTo(events.Skip(1));
        }

        [TestMethod]
        public async Task FindIdByUniqueIndexedProperty_returns_null_if_property_not_found()
        {
            string value = _fixture.Create("username");
            Guid? actual = await
                _sut.FindIdByUniqueIndexedProperty<FakeUser>("Username", value);
            actual.Should().NotHaveValue();
        }

        [TestMethod]
        public async Task FindIdByUniqueIndexedProperty_returns_aggregate_id_if_property_found()
        {
            FakeUserCreated created = _fixture.Create<FakeUserCreated>();
            RaiseEvents(_userId, created);
            await _sut.SaveEvents<FakeUser>(new[] { created });

            Guid? actual = await
                _sut.FindIdByUniqueIndexedProperty<FakeUser>("Username", created.Username);

            actual.Should().Be(_userId);
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
                events[i].RaisedAt = DateTime.UtcNow;
            }
        }
    }
}
