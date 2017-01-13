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
using ReactiveArchitecture.EventSourcing.Messaging;
using ReactiveArchitecture.FakeDomain;
using ReactiveArchitecture.FakeDomain.Events;
using Xunit;
using Xunit.Abstractions;

namespace ReactiveArchitecture.EventSourcing.Sql
{
    public class SqlEventPublisher_features : IDisposable
    {
        public class DataContext : EventStoreDbContext
        {
        }

        private ITestOutputHelper output;
        private IFixture fixture;
        private JsonMessageSerializer serializer;
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

            mockDbContext.Events = Mock.Of<DbSet<Event>>();
            Mock.Get(mockDbContext.Events).SetupData();

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
                db.Database.ExecuteSqlCommand("DELETE FROM Events");
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

            using (var db = new DataContext())
            {
                foreach (DomainEvent e in events)
                {
                    db.PendingEvents.Add(new PendingEvent
                    {
                        AggregateId = sourceId,
                        Version = e.Version,
                        PayloadJson = serializer.Serialize(e)
                    });
                }
                await db.SaveChangesAsync();
            }

            List<object> batch = null;

            Mock.Get(messageBus)
                .Setup(x => x.SendBatch(It.IsAny<IEnumerable<object>>()))
                .Callback<IEnumerable<object>>(b => batch = b.ToList())
                .Returns(Task.FromResult(true));

            // Act
            await sut.PublishPendingEvents<FakeUser>(sourceId);

            // Assert
            Mock.Get(messageBus).Verify(
                x =>
                x.SendBatch(It.IsAny<IEnumerable<object>>()),
                Times.Once());
            batch.Should().OnlyContain(e => e is IDomainEvent);
            batch.Cast<IDomainEvent>().Should().BeInAscendingOrder(e => e.Version);
            batch.ShouldAllBeEquivalentTo(events);
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
                    db.PendingEvents.Add(new PendingEvent
                    {
                        AggregateId = sourceId,
                        Version = e.Version,
                        PayloadJson = serializer.Serialize(e)
                    });
                }
                await db.SaveChangesAsync();
            }

            // Act
            await sut.PublishPendingEvents<FakeUser>(sourceId);

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

            var sut = new SqlEventPublisher(
                () => mockDbContext, serializer, messageBus);

            // Act
            await sut.PublishPendingEvents<FakeUser>(sourceId);

            // Assert
            Mock.Get(mockDbContext)
                .Verify(x => x.SaveChangesAsync(), Times.Once());
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
