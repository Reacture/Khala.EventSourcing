namespace Khala.EventSourcing.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Khala.FakeDomain;
    using Khala.FakeDomain.Events;
    using Khala.Messaging;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Xunit;

    public class SqlEventStore_specs
    {
        private static readonly DbContextOptions _dbContextOptions;

        public class DbContextSpy : FakeEventStoreDbContext
        {
            private int _commitCount = 0;

            public int CommitCount => _commitCount;

            public DbContextSpy(DbContextOptions options)
                : base(options)
            {
            }

            public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _commitCount);
                return base.SaveChangesAsync(cancellationToken);
            }
        }

        static SqlEventStore_specs()
        {
            _dbContextOptions = new DbContextOptionsBuilder()
                .UseSqlServer($@"Server=(localdb)\mssqllocaldb;Database={typeof(SqlEventStore_specs).FullName}.Core;Trusted_Connection=True;")
                .Options;

            using (var context = new FakeEventStoreDbContext(_dbContextOptions))
            {
                context.Database.Migrate();
                context.Database.ExecuteSqlCommand("DELETE FROM Aggregates");
                context.Database.ExecuteSqlCommand("DELETE FROM PersistentEvents");
                context.Database.ExecuteSqlCommand("DELETE FROM PendingEvents");
                context.Database.ExecuteSqlCommand("DELETE FROM UniqueIndexedProperties");
            }
        }

        [Fact]
        public void sut_implements_ISqlEventStore()
        {
            typeof(SqlEventStore).Should().Implement<ISqlEventStore>();
        }

        [Fact]
        public async Task SaveEvents_commits_once()
        {
            var userId = Guid.NewGuid();
            var events = new DomainEvent[]
            {
                new FakeUserCreated(),
                new FakeUsernameChanged()
            };
            events.Raise(userId);
            var context = new DbContextSpy(_dbContextOptions);
            var sut = new SqlEventStore(
                () => context,
                new JsonMessageSerializer());

            await sut.SaveEvents<FakeUser>(
                events,
                correlationId: default,
                contributor: default,
                cancellationToken: default);

            context.CommitCount.Should().Be(1);
        }

        [Fact]
        public async Task SaveEvents_does_not_commit_for_empty_events()
        {
            var context = new DbContextSpy(_dbContextOptions);
            var sut = new SqlEventStore(
                () => context,
                new JsonMessageSerializer());

            await sut.SaveEvents<FakeUser>(
                Enumerable.Empty<IDomainEvent>(),
                correlationId: default,
                contributor: default,
                cancellationToken: default);

            context.CommitCount.Should().Be(0);
        }

        [Fact]
        public void SaveEvents_fails_if_events_contains_null()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var created = new FakeUserCreated();
            created.Raise(userId);
            var events = new DomainEvent[] { created, null };

            var sut = new SqlEventStore(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());

            // Act
            Func<Task> action = () => sut.SaveEvents<FakeUser>(
                events,
                correlationId: default,
                contributor: default,
                cancellationToken: default);

            // Assert
            action.ShouldThrow<ArgumentException>().Where(x => x.ParamName == "events");
        }

        [Fact]
        public async Task SaveEvents_inserts_Aggregate_correctly_for_new_aggregate_id()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var created = new FakeUserCreated();
            var events = new DomainEvent[] { created };
            events.Raise(userId);

            var sut = new SqlEventStore(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());

            // Act
            await sut.SaveEvents<FakeUser>(
                events,
                correlationId: default,
                contributor: default,
                cancellationToken: default);

            // Assert
            using (var db = new FakeEventStoreDbContext(_dbContextOptions))
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

        [Fact]
        public async Task SaveEvents_updates_Aggregate_correctly_for_existing_aggregate_id()
        {
            // Arrange
            var userId = Guid.NewGuid();

            using (var db = new FakeEventStoreDbContext(_dbContextOptions))
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

            var usernameChanged = new FakeUsernameChanged();
            usernameChanged.Raise(userId, 1);

            var sut = new SqlEventStore(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());

            // Act
            await sut.SaveEvents<FakeUser>(
                new[] { usernameChanged },
                correlationId: default,
                contributor: default,
                cancellationToken: default);

            // Assert
            using (var db = new FakeEventStoreDbContext(_dbContextOptions))
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
            // Arrange
            var events = new DomainEvent[]
            {
                new FakeUserCreated { Version = 1 },
                new FakeUsernameChanged { Version = 2 },
                new FakeUsernameChanged { Version = 4 }
            };

            var sut = new SqlEventStore(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());

            // Act
            Func<Task> action = () => sut.SaveEvents<FakeUser>(
                events,
                correlationId: default,
                contributor: default,
                cancellationToken: default);

            // Assert
            action.ShouldThrow<ArgumentException>().Where(x => x.ParamName == "events");
        }

        [Fact]
        public async Task SaveEvents_fails_if_version_of_first_event_not_follows_aggregate()
        {
            // Arrange
            var userId = Guid.NewGuid();

            using (var db = new FakeEventStoreDbContext(_dbContextOptions))
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

            var usernameChanged = new FakeUsernameChanged();
            usernameChanged.Raise(userId, 2);

            var sut = new SqlEventStore(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());

            // Act
            Func<Task> action = () => sut.SaveEvents<FakeUser>(
                new[] { usernameChanged },
                correlationId: default,
                contributor: default,
                cancellationToken: default);

            // Assert
            action.ShouldThrow<ArgumentException>().Where(x => x.ParamName == "events");
        }

        [Fact]
        public void SaveEvents_fails_if_events_not_have_same_source_id()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var created = new FakeUserCreated
            {
                SourceId = userId,
                Version = 1,
                RaisedAt = DateTimeOffset.Now
            };

            var usernameChanged = new FakeUsernameChanged
            {
                SourceId = Guid.NewGuid(),
                Version = 2,
                RaisedAt = DateTimeOffset.Now
            };

            var sut = new SqlEventStore(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());

            // Act
            Func<Task> action = () => sut.SaveEvents<FakeUser>(
                new DomainEvent[] { created, usernameChanged },
                correlationId: default,
                contributor: default,
                cancellationToken: default);

            // Assert
            action.ShouldThrow<ArgumentException>().Where(x => x.ParamName == "events");
        }

        [Fact]
        public async Task SaveEvents_saves_pending_events_correctly()
        {
            // Arrange
            var userId = Guid.NewGuid();

            var created = new FakeUserCreated();
            var usernameChanged = new FakeUsernameChanged();
            var events = new DomainEvent[] { created, usernameChanged };
            events.Raise(userId);

            var serializer = new JsonMessageSerializer();

            var sut = new SqlEventStore(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                serializer);

            var correlationId = Guid.NewGuid();
            string contributor = Guid.NewGuid().ToString();

            // Act
            await sut.SaveEvents<FakeUser>(events, correlationId, contributor, default);

            // Asseert
            using (var db = new FakeEventStoreDbContext(_dbContextOptions))
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
                        t.Pending.Contributor,
                        Message = serializer.Deserialize(t.Pending.EventJson)
                    };
                    actual.ShouldBeEquivalentTo(new
                    {
                        t.Source.Version,
                        CorrelationId = correlationId,
                        Contributor = contributor,
                        Message = t.Source
                    },
                    opts => opts.RespectingRuntimeTypes());
                }
            }
        }

        [Fact]
        public async Task SaveEvents_saves_events_correctly()
        {
            // Arrange
            var userId = Guid.NewGuid();

            var created = new FakeUserCreated();
            var usernameChanged = new FakeUsernameChanged();
            var events = new DomainEvent[] { created, usernameChanged };
            events.Raise(userId);

            var serializer = new JsonMessageSerializer();

            var sut = new SqlEventStore(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                serializer);

            // Act
            await sut.SaveEvents<FakeUser>(
                events,
                correlationId: default,
                contributor: default,
                cancellationToken: default);

            // Asseert
            using (var db = new FakeEventStoreDbContext(_dbContextOptions))
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

        [Fact]
        public async Task SaveEvents_sets_message_properties_correctly()
        {
            // Arrange
            var userId = Guid.NewGuid();

            var created = new FakeUserCreated();
            var usernameChanged = new FakeUsernameChanged();
            var events = new DomainEvent[] { created, usernameChanged };
            events.Raise(userId);

            var sut = new SqlEventStore(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());

            // Act
            await sut.SaveEvents<FakeUser>(
                events,
                correlationId: Guid.NewGuid(),
                contributor: Guid.NewGuid().ToString(),
                cancellationToken: default);

            // Asseert
            using (var db = new FakeEventStoreDbContext(_dbContextOptions))
            {
                IEnumerable<object> expected = db
                    .PendingEvents
                    .Where(e => e.AggregateId == userId)
                    .OrderBy(e => e.Version)
                    .AsEnumerable()
                    .Select(e => new { e.MessageId, e.CorrelationId, e.Contributor })
                    .ToList();

                IEnumerable<object> actual = db
                    .PersistentEvents
                    .Where(e => e.AggregateId == userId)
                    .OrderBy(e => e.Version)
                    .AsEnumerable()
                    .Select(e => new { e.MessageId, e.CorrelationId, e.Contributor })
                    .ToList();

                actual.ShouldAllBeEquivalentTo(expected);
            }
        }

        [Fact]
        public async Task SaveEvents_inserts_UniqueIndexedProperty_for_new_property()
        {
            // Arrange
            var userId = Guid.NewGuid();

            var created = new FakeUserCreated();
            created.Raise(userId);

            var sut = new SqlEventStore(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());

            // Act
            await sut.SaveEvents<FakeUser>(
                new[] { created },
                correlationId: default,
                contributor: default,
                cancellationToken: default);

            // Assert
            using (var db = new FakeEventStoreDbContext(_dbContextOptions))
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

        [Fact]
        public async Task SaveEvents_inserts_UniqueIndexedProperty_with_value_of_latest_indexed_event()
        {
            // Arrange
            var userId = Guid.NewGuid();

            var created = new FakeUserCreated();
            var usernameChanged = new FakeUsernameChanged();
            var events = new DomainEvent[] { created, usernameChanged };
            events.Raise(userId);

            var sut = new SqlEventStore(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());

            // Act
            await sut.SaveEvents<FakeUser>(
                events,
                correlationId: default,
                contributor: default,
                cancellationToken: default);

            // Assert
            using (var db = new FakeEventStoreDbContext(_dbContextOptions))
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
            // Arrange
            var userId = Guid.NewGuid();

            var created = new FakeUserCreated { Username = null };
            created.Raise(userId);

            var sut = new SqlEventStore(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());

            // Act
            await sut.SaveEvents<FakeUser>(
                new DomainEvent[] { created },
                correlationId: default,
                contributor: default,
                cancellationToken: default);

            // Assert
            using (var db = new FakeEventStoreDbContext(_dbContextOptions))
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

        [Fact]
        public async Task SaveEvents_removes_existing_UniqueIndexedProperty_if_property_value_is_null()
        {
            // Arrange
            var userId = Guid.NewGuid();

            var sut = new SqlEventStore(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());

            var created = new FakeUserCreated();
            created.Raise(userId);
            await sut.SaveEvents<FakeUser>(new[] { created });

            var usernameChanged = new FakeUsernameChanged { Username = null };
            usernameChanged.Raise(userId, 1);

            // Act
            await sut.SaveEvents<FakeUser>(
                new[] { usernameChanged },
                correlationId: default,
                contributor: default,
                cancellationToken: default);

            // Assert
            using (var db = new FakeEventStoreDbContext(_dbContextOptions))
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

        [Fact]
        public async Task SaveEvents_fails_if_unique_indexed_property_duplicate()
        {
            // Arrange
            var sut = new SqlEventStore(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());

            string duplicateUserName = Guid.NewGuid().ToString();

            var macId = Guid.NewGuid();
            var macCreated = new FakeUserCreated { Username = duplicateUserName };
            macCreated.Raise(macId);
            await sut.SaveEvents<FakeUser>(new[] { macCreated });

            // Act
            var toshId = Guid.NewGuid();
            var toshCreated = new FakeUserCreated { Username = duplicateUserName };
            toshCreated.Raise(toshId);
            Func<Task> action = () => sut.SaveEvents<FakeUser>(
                new[] { toshCreated },
                correlationId: default,
                contributor: default,
                cancellationToken: default);

            // Assert
            action.ShouldThrow<Exception>();
            using (var db = new FakeEventStoreDbContext(_dbContextOptions))
            {
                IQueryable<PersistentEvent> query =
                    from e in db.PersistentEvents
                    where e.AggregateId == toshId
                    select e;
                (await query.AnyAsync()).Should().BeFalse();
            }
        }

        [Fact]
        public async Task SaveEvents_inserts_Correlation_entity_correctly()
        {
            // Arrange
            var userId = Guid.NewGuid();

            var created = new FakeUserCreated();
            var correlationId = Guid.NewGuid();
            created.Raise(userId);
            var now = DateTimeOffset.Now;

            var sut = new SqlEventStore(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());

            // Act
            await sut.SaveEvents<FakeUser>(
                new[] { created },
                correlationId,
                contributor: default,
                cancellationToken: default);

            // Assert
            using (var db = new FakeEventStoreDbContext(_dbContextOptions))
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

        [Fact]
        public async Task SaveEvents_throws_DuplicateCorrelationException_if_correlation_duplicate()
        {
            // Arrange
            var userId = Guid.NewGuid();

            var created = new FakeUserCreated();
            var usernameChanged = new FakeUsernameChanged();
            new DomainEvent[] { created, usernameChanged }.Raise(userId);

            var sut = new SqlEventStore(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());

            var correlationId = Guid.NewGuid();

            await sut.SaveEvents<FakeUser>(
                new[] { created },
                correlationId,
                contributor: default,
                cancellationToken: default);

            // Act
            Func<Task> action = () => sut.SaveEvents<FakeUser>(new[] { usernameChanged }, correlationId);

            // Assert
            action.ShouldThrow<DuplicateCorrelationException>().Where(
                x =>
                x.SourceType == typeof(FakeUser) &&
                x.SourceId == userId &&
                x.CorrelationId == correlationId &&
                x.InnerException is DbUpdateException);
        }

        [Fact]
        public void SaveEvents_fails_if_db_context_does_not_support_transaction()
        {
            // Arrange
            var created = new FakeUserCreated { Version = 1 };

            string databaseName = nameof(SaveEvents_fails_if_db_context_does_not_support_transaction);
            DbContextOptions options = new DbContextOptionsBuilder()
                .UseInMemoryDatabase(databaseName)
                .Options;
            var sut = new SqlEventStore(
                () => new FakeEventStoreDbContext(options),
                new JsonMessageSerializer());

            // Act
            Func<Task> action = () => sut.SaveEvents<FakeUser>(
                new[] { created },
                correlationId: default,
                contributor: default,
                cancellationToken: default);

            // Assert
            action.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void SaveEvents_succeeds_if_db_context_supports_transaction()
        {
            // Arrange
            var created = new FakeUserCreated { Version = 1 };

            string databaseName = nameof(SaveEvents_succeeds_if_db_context_supports_transaction);
            DbContextOptions options = new DbContextOptionsBuilder()
                .UseInMemoryDatabase(databaseName)
                .ConfigureWarnings(builder => builder.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            var sut = new SqlEventStore(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());

            // Act
            Func<Task> action = () => sut.SaveEvents<FakeUser>(
                new[] { created },
                correlationId: default,
                contributor: default,
                cancellationToken: default);

            // Assert
            action.ShouldNotThrow();
        }

        [Fact]
        public async Task LoadEvents_restores_all_events_correctly()
        {
            // Arrange
            var userId = Guid.NewGuid();

            var created = new FakeUserCreated();
            var usernameChanged = new FakeUsernameChanged();
            var events = new DomainEvent[] { created, usernameChanged };
            events.Raise(userId);

            var sut = new SqlEventStore(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());

            await sut.SaveEvents<FakeUser>(events);

            // Act
            IEnumerable<IDomainEvent> actual = await sut.LoadEvents<FakeUser>(userId);

            // Assert
            actual.ShouldAllBeEquivalentTo(events);
        }

        [Fact]
        public async Task LoadEvents_restores_events_after_specified_version_correctly()
        {
            // Arrange
            var userId = Guid.NewGuid();

            var created = new FakeUserCreated();
            var usernameChanged = new FakeUsernameChanged();
            var events = new DomainEvent[] { created, usernameChanged };
            events.Raise(userId);

            var sut = new SqlEventStore(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());

            await sut.SaveEvents<FakeUser>(events);

            // Act
            IEnumerable<IDomainEvent> actual = await sut.LoadEvents<FakeUser>(userId, 1);

            // Assert
            actual.ShouldAllBeEquivalentTo(events.Skip(1));
        }

        [Fact]
        public async Task FindIdByUniqueIndexedProperty_returns_null_if_property_not_found()
        {
            string value = Guid.NewGuid().ToString();
            var sut = new SqlEventStore(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());

            Guid? actual = await sut.FindIdByUniqueIndexedProperty<FakeUser>("Username", value);

            actual.Should().NotHaveValue();
        }

        [Fact]
        public async Task FindIdByUniqueIndexedProperty_returns_aggregate_id_if_property_found()
        {
            var userId = Guid.NewGuid();
            var created = new FakeUserCreated();
            created.Raise(userId);
            var sut = new SqlEventStore(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());
            await sut.SaveEvents<FakeUser>(new[] { created });

            Guid? actual = await sut.FindIdByUniqueIndexedProperty<FakeUser>("Username", created.Username);

            actual.Should().Be(userId);
        }
    }
}
