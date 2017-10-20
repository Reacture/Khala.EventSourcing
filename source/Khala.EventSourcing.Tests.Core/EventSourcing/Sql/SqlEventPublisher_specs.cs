namespace Khala.EventSourcing.Sql
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Khala.FakeDomain;
    using Khala.FakeDomain.Events;
    using Khala.Messaging;
    using Microsoft.EntityFrameworkCore;
    using Moq;
    using Xunit;

    public class SqlEventPublisher_specs
    {
        private static readonly DbContextOptions _dbContextOptions;

        static SqlEventPublisher_specs()
        {
            _dbContextOptions = new DbContextOptionsBuilder()
                .UseSqlServer($@"Server=(localdb)\mssqllocaldb;Database={typeof(SqlEventPublisher_specs).FullName}.Core;Trusted_Connection=True;")
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
        public void sut_implements_IEventPublisher()
        {
            typeof(SqlEventPublisher).Should().Implement<ISqlEventPublisher>();
        }

        [Fact]
        public async Task FlushPendingEvents_sends_events()
        {
            // Arrange
            var created = new FakeUserCreated();
            var usernameChanged = new FakeUsernameChanged();
            var sourceId = Guid.NewGuid();

            var events = new DomainEvent[] { created, usernameChanged };
            events.Raise(sourceId);

            var envelopes = new List<Envelope>();

            using (var db = new FakeEventStoreDbContext(_dbContextOptions))
            {
                var serializer = new JsonMessageSerializer();
                foreach (DomainEvent e in events)
                {
                    var envelope = new Envelope(e);
                    envelopes.Add(envelope);
                    db.PendingEvents.Add(PendingEvent.FromEnvelope(envelope, serializer));
                }

                await db.SaveChangesAsync();
            }

            var messageBus = new MessageBus();

            var sut = new SqlEventPublisher(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer(),
                messageBus);

            // Act
            await sut.FlushPendingEvents(sourceId, CancellationToken.None);

            // Assert
            messageBus.Sent.ShouldAllBeEquivalentTo(envelopes, opts => opts.RespectingRuntimeTypes());
        }

        [Fact]
        public async Task FlushPendingEvents_does_not_invoke_Send_if_pending_event_not_found()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var messageBus = Mock.Of<IMessageBus>();
            var sut = new SqlEventPublisher(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer(),
                messageBus);

            // Act
            await sut.FlushPendingEvents(sourceId, default);

            // Assert
            Mock.Get(messageBus).Verify(
                x =>
                x.Send(It.IsAny<IEnumerable<Envelope>>(), default),
                Times.Never());
        }

        [Fact]
        public async Task FlushPendingEvents_deletes_pending_events()
        {
            // Arrange
            var sourceId = Guid.NewGuid();

            var created = new FakeUserCreated();
            var usernameChanged = new FakeUsernameChanged();
            var events = new DomainEvent[] { created, usernameChanged };
            events.Raise(sourceId);

            using (var db = new FakeEventStoreDbContext(_dbContextOptions))
            {
                var serializer = new JsonMessageSerializer();
                foreach (DomainEvent e in events)
                {
                    var envelope = new Envelope(e);
                    db.PendingEvents.Add(PendingEvent.FromEnvelope(envelope, serializer));
                }

                await db.SaveChangesAsync();
            }

            var sut = new SqlEventPublisher(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer(),
                Mock.Of<IMessageBus>());

            // Act
            await sut.FlushPendingEvents(sourceId, CancellationToken.None);

            // Assert
            using (var db = new FakeEventStoreDbContext(_dbContextOptions))
            {
                bool actual = await db
                    .PendingEvents
                    .Where(e => e.AggregateId == sourceId)
                    .AnyAsync();
                actual.Should().BeFalse();
            }
        }

        [Fact]
        public async Task FlushAllPendingEvents_sends_app_pending_events()
        {
            // Arrange
            IEnumerable<FakeUserCreated> createdEvents = new[]
            {
                new FakeUserCreated(),
                new FakeUserCreated(),
                new FakeUserCreated()
            };

            var eventStore = new SqlEventStore(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());

            foreach (FakeUserCreated createdEvent in createdEvents)
            {
                createdEvent.Raise(Guid.NewGuid());
                await eventStore.SaveEvents<FakeUser>(new[] { createdEvent });
            }

            var messageBus = new MessageBus();

            var sut = new SqlEventPublisher(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer(),
                messageBus);

            // Act
            await sut.FlushAllPendingEvents(CancellationToken.None);

            // Assert
            foreach (FakeUserCreated createdEvent in createdEvents)
            {
                messageBus
                    .Sent
                    .Select(e => e.Message)
                    .OfType<FakeUserCreated>()
                    .Where(e => e.SourceId == createdEvent.SourceId)
                    .Should()
                    .ContainSingle()
                    .Which
                    .ShouldBeEquivalentTo(createdEvent);
            }
        }

        [Fact]
        public async Task FlushAllPendingEvents_deletes_all_pending_events()
        {
            // Arrange
            IEnumerable<FakeUserCreated> createdEvents = new[]
            {
                new FakeUserCreated(),
                new FakeUserCreated(),
                new FakeUserCreated()
            };

            var eventStore = new SqlEventStore(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer());

            foreach (FakeUserCreated createdEvent in createdEvents)
            {
                createdEvent.Raise(Guid.NewGuid());
                await eventStore.SaveEvents<FakeUser>(new[] { createdEvent });
            }

            var sut = new SqlEventPublisher(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                new JsonMessageSerializer(),
                Mock.Of<IMessageBus>());

            // Act
            await sut.FlushAllPendingEvents(CancellationToken.None);

            // Assert
            using (var db = new FakeEventStoreDbContext(_dbContextOptions))
            {
                foreach (FakeUserCreated createdEvent in createdEvents)
                {
                    Guid userId = createdEvent.SourceId;
                    IQueryable<PendingEvent> query = from e in db.PendingEvents
                                                     where e.AggregateId == userId
                                                     select e;
                    (await query.AnyAsync()).Should().BeFalse();
                }
            }
        }

        [Fact]
        public async Task FlushPendingEvents_absorbs_exception_caused_by_that_some_pending_event_already_deleted_since_loaded()
        {
            // Arrange
            var user = new FakeUser(id: Guid.NewGuid(), username: Guid.NewGuid().ToString());
            user.ChangeUsername(username: Guid.NewGuid().ToString());

            var serializer = new JsonMessageSerializer();
            var completionSource = new TaskCompletionSource<bool>();
            IMessageBus messageBus = new AwaitingMessageBus(completionSource.Task);

            await new SqlEventStore(() => new FakeEventStoreDbContext(_dbContextOptions), serializer).SaveEvents<FakeUser>(user.FlushPendingEvents());

            var sut = new SqlEventPublisher(
                () => new FakeEventStoreDbContext(_dbContextOptions),
                serializer,
                messageBus);

            // Act
            Func<Task> action = async () =>
            {
                Task flushTask = sut.FlushPendingEvents(user.Id, CancellationToken.None);

                using (var db = new FakeEventStoreDbContext(_dbContextOptions))
                {
                    List<PendingEvent> pendingEvents = await db
                        .PendingEvents
                        .Where(e => e.AggregateId == user.Id)
                        .OrderBy(e => e.Version)
                        .Take(1)
                        .ToListAsync();
                    db.PendingEvents.RemoveRange(pendingEvents);
                    await db.SaveChangesAsync();
                }

                completionSource.SetResult(true);
                await flushTask;
            };

            // Assert
            action.ShouldNotThrow();
            using (var db = new FakeEventStoreDbContext(_dbContextOptions))
            {
                (await db.PendingEvents.AnyAsync(e => e.AggregateId == user.Id))
                .Should().BeFalse("all pending events should be deleted");
            }
        }

        private class MessageBus : IMessageBus
        {
            private readonly ConcurrentQueue<Envelope> _sent = new ConcurrentQueue<Envelope>();

            public IReadOnlyCollection<Envelope> Sent => _sent;

            public Task Send(Envelope envelope, CancellationToken cancellationToken)
            {
                _sent.Enqueue(envelope);
                return Task.CompletedTask;
            }

            public Task Send(IEnumerable<Envelope> envelopes, CancellationToken cancellationToken)
            {
                foreach (Envelope envelope in envelopes)
                {
                    _sent.Enqueue(envelope);
                }

                return Task.CompletedTask;
            }
        }

        private class AwaitingMessageBus : IMessageBus
        {
            private readonly Task _awaitable;

            public AwaitingMessageBus(Task awaitable)
            {
                _awaitable = awaitable;
            }

            public Task Send(Envelope envelope, CancellationToken cancellationToken)
            {
                return _awaitable;
            }

            public Task Send(IEnumerable<Envelope> envelopes, CancellationToken cancellationToken)
            {
                return _awaitable;
            }
        }
    }
}
