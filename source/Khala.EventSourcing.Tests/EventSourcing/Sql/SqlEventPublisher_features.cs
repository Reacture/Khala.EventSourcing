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
using Moq;
using Ploeh.AutoFixture;
using Ploeh.AutoFixture.AutoMoq;
using Ploeh.AutoFixture.Idioms;
using Ploeh.AutoFixture.Xunit2;
using Xunit;
using Xunit.Abstractions;

namespace Khala.EventSourcing.Sql
{
    public class SqlEventPublisher_features : IDisposable
    {
        public class DataContext : EventStoreDbContext
        {
        }

        private ITestOutputHelper output;
        private IFixture fixture;
        private IMessageSerializer serializer;
        private IMessageBus messageBus;
        private SqlEventPublisher sut;
        private EventStoreDbContext mockDbContext;

        public SqlEventPublisher_features(ITestOutputHelper output)
        {
            this.output = output;

            fixture = new Fixture().Customize(new AutoMoqCustomization());
            fixture.Inject<Func<EventStoreDbContext>>(() => new DataContext());

            serializer = new JsonMessageSerializer();

            messageBus = Mock.Of<IMessageBus>();

            sut = new SqlEventPublisher(
                () => new DataContext(), serializer, messageBus);

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
        public void sut_implements_IEventPublisher()
        {
            sut.Should().BeAssignableTo<ISqlEventPublisher>();
        }

        [Fact]
        public void class_has_guard_clauses()
        {
            var assertion = new GuardClauseAssertion(fixture);
            assertion.Verify(typeof(SqlEventPublisher));
        }

        [Theory]
        [AutoData]
        public async Task PublishPendingEvents_sends_events(
            FakeUserCreated created,
            FakeUsernameChanged usernameChanged)
        {
            // Arrange
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
                    db.PendingEvents.Add(PendingEvent.FromEnvelope(envelope, serializer));
                }
                await db.SaveChangesAsync();
            }

            List<Envelope> batch = null;

            Mock.Get(messageBus)
                .Setup(
                    x =>
                    x.SendBatch(
                        It.IsAny<IEnumerable<Envelope>>(),
                        It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<Envelope>, CancellationToken>((b, t) => batch = b.ToList())
                .Returns(Task.FromResult(true));

            // Act
            await sut.PublishPendingEvents(sourceId, CancellationToken.None);

            // Assert
            Mock.Get(messageBus).Verify(
                x =>
                x.SendBatch(
                    It.IsAny<IEnumerable<Envelope>>(),
                    CancellationToken.None),
                Times.Once());
            batch.ShouldAllBeEquivalentTo(envelopes, opts => opts.RespectingRuntimeTypes());
        }

        [Fact]
        public async Task PublishEvents_does_not_invoke_SendBatch_if_pending_event_not_found()
        {
            var sourceId = Guid.NewGuid();
            await sut.PublishPendingEvents(sourceId, CancellationToken.None);
            Mock.Get(messageBus).Verify(
                x =>
                x.SendBatch(
                    It.IsAny<IEnumerable<Envelope>>(),
                    CancellationToken.None),
                Times.Never());
        }

        [Theory]
        [AutoData]
        public async Task PublishPendingEvents_deletes_pending_events(
            FakeUserCreated created,
            FakeUsernameChanged usernameChanged)
        {
            // Arrange
            var sourceId = Guid.NewGuid();

            var events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(sourceId, events);

            using (var db = new DataContext())
            {
                foreach (DomainEvent e in events)
                {
                    var envelope = new Envelope(e);
                    db.PendingEvents.Add(PendingEvent.FromEnvelope(envelope, serializer));
                }
                await db.SaveChangesAsync();
            }

            // Act
            await sut.PublishPendingEvents(sourceId, CancellationToken.None);

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

        [Theory]
        [AutoData]
        public async Task PublishPendingEvents_commits_once(
            FakeUserCreated created,
            FakeUsernameChanged usernameChanged)
        {
            // Arrange
            var sourceId = Guid.NewGuid();

            var events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(sourceId, events);

            Mock.Get(mockDbContext.PendingEvents)
                .SetupData(events
                .Select(e => new Envelope(e))
                .Select(e => PendingEvent.FromEnvelope(e, serializer))
                .ToList());

            var sut = new SqlEventPublisher(
                () => mockDbContext, serializer, messageBus);

            // Act
            await sut.PublishPendingEvents(sourceId, CancellationToken.None);

            // Assert
            Mock.Get(mockDbContext).Verify(
                x => x.SaveChangesAsync(CancellationToken.None),
                Times.Once());
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
