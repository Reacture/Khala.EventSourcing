namespace Khala.EventSourcing.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Data.Entity;
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
    public class SqlEventPublisher_specs
    {
        private class DataContext : EventStoreDbContext
        {
        }

        private IFixture _fixture;
        private IMessageSerializer _serializer;
        private IMessageBus _messageBus;
        private SqlEventPublisher _sut;

        public TestContext TestContext { get; set; }

        public EventStoreDbContext CreateDbContext()
        {
            var context = new DataContext();
            context.Database.Log = m => TestContext?.WriteLine(m);
            return context;
        }

        [TestInitialize]
        public void TestInitialize()
        {
            _fixture = new Fixture().Customize(new AutoMoqCustomization());
            _fixture.Inject<Func<EventStoreDbContext>>(CreateDbContext);

            _serializer = new JsonMessageSerializer();

            _messageBus = Mock.Of<IMessageBus>();

            _sut = new SqlEventPublisher(CreateDbContext, _serializer, _messageBus);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            using (EventStoreDbContext db = CreateDbContext())
            {
                db.Database.ExecuteSqlCommand("DELETE FROM Aggregates");
                db.Database.ExecuteSqlCommand("DELETE FROM PersistentEvents");
                db.Database.ExecuteSqlCommand("DELETE FROM PendingEvents");
                db.Database.ExecuteSqlCommand("DELETE FROM UniqueIndexedProperties");
            }
        }

        [TestMethod]
        public void sut_implements_IEventPublisher()
        {
            _sut.Should().BeAssignableTo<ISqlEventPublisher>();
        }

        [TestMethod]
        public void class_has_guard_clauses()
        {
            var assertion = new GuardClauseAssertion(_fixture);
            assertion.Verify(typeof(SqlEventPublisher));
        }

        [TestMethod]
        public async Task FlushPendingEvents_sends_events()
        {
            // Arrange
            var created = _fixture.Create<FakeUserCreated>();
            var usernameChanged = _fixture.Create<FakeUsernameChanged>();
            var sourceId = Guid.NewGuid();

            var events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(sourceId, events);

            var envelopes = new List<Envelope>();

            using (var db = new DataContext())
            {
                foreach (DomainEvent e in events)
                {
                    var envelope = new Envelope(e);
                    envelopes.Add(envelope);
                    db.PendingEvents.Add(PendingEvent.FromEnvelope(envelope, _serializer));
                }

                await db.SaveChangesAsync();
            }

            List<Envelope> batch = null;

            Mock.Get(_messageBus)
                .Setup(
                    x =>
                    x.Send(
                        It.IsAny<IEnumerable<Envelope>>(),
                        It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<Envelope>, CancellationToken>((b, t) => batch = b.ToList())
                .Returns(Task.FromResult(true));

            // Act
            await _sut.FlushPendingEvents(sourceId, CancellationToken.None);

            // Assert
            Mock.Get(_messageBus).Verify(
                x =>
                x.Send(
                    It.IsAny<IEnumerable<Envelope>>(),
                    CancellationToken.None),
                Times.Once());
            batch.ShouldAllBeEquivalentTo(envelopes, opts => opts.RespectingRuntimeTypes());
        }

        [TestMethod]
        public async Task FlushPendingEvents_does_not_invoke_SendBatch_if_pending_event_not_found()
        {
            var sourceId = Guid.NewGuid();
            await _sut.FlushPendingEvents(sourceId, CancellationToken.None);
            Mock.Get(_messageBus).Verify(
                x =>
                x.Send(
                    It.IsAny<IEnumerable<Envelope>>(),
                    CancellationToken.None),
                Times.Never());
        }

        [TestMethod]
        public async Task FlushPendingEvents_deletes_pending_events()
        {
            // Arrange
            var created = _fixture.Create<FakeUserCreated>();
            var usernameChanged = _fixture.Create<FakeUsernameChanged>();
            var sourceId = Guid.NewGuid();

            var events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(sourceId, events);

            using (var db = new DataContext())
            {
                foreach (DomainEvent e in events)
                {
                    var envelope = new Envelope(e);
                    db.PendingEvents.Add(PendingEvent.FromEnvelope(envelope, _serializer));
                }

                await db.SaveChangesAsync();
            }

            // Act
            await _sut.FlushPendingEvents(sourceId, CancellationToken.None);

            // Assert
            using (var db = new DataContext())
            {
                bool actual = await db
                    .PendingEvents
                    .Where(e => e.AggregateId == sourceId)
                    .AnyAsync();
                actual.Should().BeFalse();
            }
        }

        [TestMethod]
        public async Task FlushAllPendingEvents_sends_app_pending_events()
        {
            // Arrange
            IEnumerable<FakeUserCreated> createdEvents = _fixture
                .Build<FakeUserCreated>()
                .With(e => e.Version, 1)
                .CreateMany();
            var eventStore = new SqlEventStore(() => new DataContext(), _serializer);
            foreach (FakeUserCreated createdEvent in createdEvents)
            {
                await eventStore.SaveEvents<FakeUser>(new[] { createdEvent });
            }

            var sentEvents = new List<Envelope>();

            Mock.Get(_messageBus)
                .Setup(
                    x =>
                    x.Send(
                        It.IsAny<IEnumerable<Envelope>>(),
                        It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<Envelope>, CancellationToken>((batch, t) => sentEvents.AddRange(batch))
                .Returns(Task.FromResult(true));

            // Act
            await _sut.FlushAllPendingEvents(CancellationToken.None);

            // Assert
            foreach (FakeUserCreated createdEvent in createdEvents)
            {
                sentEvents
                    .Select(e => e.Message)
                    .OfType<FakeUserCreated>()
                    .Where(e => e.SourceId == createdEvent.SourceId)
                    .Should()
                    .ContainSingle()
                    .Which
                    .ShouldBeEquivalentTo(createdEvent);
            }
        }

        [TestMethod]
        public async Task FlushAllPendingEvents_deletes_all_pending_events()
        {
            // Arrange
            IEnumerable<FakeUserCreated> createdEvents = _fixture
                .Build<FakeUserCreated>()
                .With(e => e.Version, 1)
                .CreateMany();
            var eventStore = new SqlEventStore(() => new DataContext(), _serializer);
            foreach (FakeUserCreated createdEvent in createdEvents)
            {
                await eventStore.SaveEvents<FakeUser>(new[] { createdEvent });
            }

            // Act
            await _sut.FlushAllPendingEvents(CancellationToken.None);

            // Assert
            using (var db = new DataContext())
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

        [TestMethod]
        public async Task FlushPendingEvents_absorbs_exception_caused_by_that_some_pending_event_already_deleted_since_loaded()
        {
            // Arrange
            var completionSource = new TaskCompletionSource<bool>();
            IMessageBus messageBus = new AwaitingMessageBus(completionSource.Task);
            var fixture = new Fixture();
            var user = fixture.Create<FakeUser>();
            user.ChangeUsername(fixture.Create(nameof(user.Username)));
            var eventStore = new SqlEventStore(CreateDbContext, _serializer);
            await eventStore.SaveEvents<FakeUser>(user.FlushPendingEvents());
            var sut = new SqlEventPublisher(CreateDbContext, _serializer, messageBus);

            // Act
            Func<Task> action = async () =>
            {
                Task flushTask = sut.FlushPendingEvents(user.Id, CancellationToken.None);

                using (EventStoreDbContext db = CreateDbContext())
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
            using (EventStoreDbContext db = CreateDbContext())
            {
                (await db.PendingEvents.AnyAsync(e => e.AggregateId == user.Id))
                .Should().BeFalse("all pending events should be deleted");
            }
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
