using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Khala.FakeDomain;
using Microsoft.WindowsAzure.Storage;
using Moq;
using Ploeh.AutoFixture;
using Ploeh.AutoFixture.AutoMoq;
using Ploeh.AutoFixture.Idioms;
using Ploeh.AutoFixture.Xunit2;
using Xunit;

namespace Khala.EventSourcing.Azure
{
    public class AzureEventSourcedRepository_features
    {
        private IFixture fixture;
        private IAzureEventStore eventStore;
        private IAzureEventPublisher eventPublisher;
        private IMementoStore mementoStore;
        private AzureEventSourcedRepository<FakeUser> sut;

        public AzureEventSourcedRepository_features()
        {
            fixture = new Fixture().Customize(new AutoMoqCustomization());
            eventStore = Mock.Of<IAzureEventStore>();
            eventPublisher = Mock.Of<IAzureEventPublisher>();
            mementoStore = Mock.Of<IMementoStore>();
            sut = new AzureEventSourcedRepository<FakeUser>(
                eventStore,
                eventPublisher,
                mementoStore,
                FakeUser.Factory,
                FakeUser.Factory);
        }

        [Fact]
        public void sut_implements_IEventSourcedRepositoryT()
        {
            sut.Should().BeAssignableTo<IEventSourcedRepository<FakeUser>>();
        }

        [Fact]
        public void class_has_guard_clauses()
        {
            var assertion = new GuardClauseAssertion(fixture);
            fixture.Inject(CloudStorageAccount.DevelopmentStorageAccount.CreateCloudTableClient().GetTableReference("foo"));
            assertion.Verify(typeof(AzureEventSourcedRepository<>));
        }

        [Theory]
        [AutoData]
        public async Task Save_saves_events(
            FakeUser user,
            Guid correlationId,
            string username)
        {
            user.ChangeUsername(username);

            await sut.Save(user, correlationId, CancellationToken.None);

            Mock.Get(eventStore).Verify(
                x =>
                x.SaveEvents<FakeUser>(
                    user.PendingEvents,
                    correlationId,
                    CancellationToken.None),
                Times.Once());
        }

        [Theory]
        [AutoData]
        public async Task Save_publishes_events(
            FakeUser user,
            Guid correlationId,
            string username)
        {
            user.ChangeUsername(username);

            await sut.Save(user, correlationId, CancellationToken.None);

            Mock.Get(eventPublisher).Verify(
                x =>
                x.PublishPendingEvents<FakeUser>(
                    user.Id,
                    CancellationToken.None),
                Times.Once());
        }

        [Theory]
        [AutoData]
        public void Save_does_not_publish_events_if_fails_to_save(
            FakeUser user,
            Guid correlationId,
            string username)
        {
            // Arrange
            user.ChangeUsername(username);
            Mock.Get(eventStore)
                .Setup(
                    x =>
                    x.SaveEvents<FakeUser>(
                        It.IsAny<IEnumerable<IDomainEvent>>(),
                        It.IsAny<Guid?>(),
                        CancellationToken.None))
                .Throws<InvalidOperationException>();

            // Act
            Func<Task> action = () => sut.Save(user, correlationId, CancellationToken.None);

            // Assert
            action.ShouldThrow<InvalidOperationException>();
            Mock.Get(eventPublisher).Verify(
                x =>
                x.PublishPendingEvents<FakeUser>(
                    user.Id,
                    CancellationToken.None),
                Times.Never());
        }

        [Theory]
        [AutoData]
        public async Task Save_saves_memento(
            FakeUser user,
            Guid correlationId,
            string username)
        {
            user.ChangeUsername(username);

            await sut.Save(user, correlationId, CancellationToken.None);

            Mock.Get(mementoStore).Verify(
                x =>
                x.Save<FakeUser>(
                    user.Id,
                    It.Is<FakeUserMemento>(
                        p =>
                        p.Version == user.Version &&
                        p.Username == user.Username),
                    CancellationToken.None),
                Times.Once());
        }

        [Theory]
        [AutoData]
        public void Save_does_not_save_memento_if_fails_to_save_events(
            FakeUser user,
            Guid correlationId,
            string username)
        {
            // Arrange
            user.ChangeUsername(username);
            Mock.Get(eventStore)
                .Setup(
                    x =>
                    x.SaveEvents<FakeUser>(
                        It.IsAny<IEnumerable<IDomainEvent>>(),
                        It.IsAny<Guid?>(),
                        CancellationToken.None))
                .Throws<InvalidOperationException>();

            // Act
            Func<Task> action = () => sut.Save(user, correlationId, CancellationToken.None);

            // Assert
            action.ShouldThrow<InvalidOperationException>();
            Mock.Get(mementoStore).Verify(
                x =>
                x.Save<FakeUser>(
                        user.Id,
                        It.IsAny<IMemento>(),
                        It.IsAny<CancellationToken>()),
                Times.Never());
        }

        [Theory]
        [AutoData]
        public async Task Find_publishes_pending_events(
            FakeUser user,
            string username)
        {
            user.ChangeUsername(username);

            await sut.Find(user.Id, CancellationToken.None);

            Mock.Get(eventPublisher).Verify(
                x =>
                x.PublishPendingEvents<FakeUser>(
                    user.Id,
                    CancellationToken.None),
                Times.Once());
        }

        [Theory]
        [AutoData]
        public async Task Find_restores_aggregate(
            FakeUser user,
            string username)
        {
            user.ChangeUsername(username);
            Mock.Get(eventStore)
                .Setup(x => x.LoadEvents<FakeUser>(user.Id, 0, CancellationToken.None))
                .ReturnsAsync(user.PendingEvents);

            FakeUser actual = await sut.Find(user.Id, CancellationToken.None);

            actual.ShouldBeEquivalentTo(user, opts => opts.Excluding(x => x.PendingEvents));
        }

        [Theory]
        [AutoData]
        public void Find_does_not_load_events_if_fails_to_publish_events(
            FakeUser user,
            string username)
        {
            // Arrange
            user.ChangeUsername(username);
            Mock.Get(eventPublisher)
                .Setup(
                    x =>
                    x.PublishPendingEvents<FakeUser>(
                        user.Id,
                        CancellationToken.None))
                .Throws<InvalidOperationException>();

            // Act
            Func<Task> action = () => sut.Find(user.Id, CancellationToken.None);

            // Assert
            action.ShouldThrow<InvalidOperationException>();
            Mock.Get(eventStore).Verify(
                x =>
                x.LoadEvents<FakeUser>(
                    user.Id,
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()),
                Times.Never());
        }

        [Theory]
        [AutoData]
        public async Task Find_returns_null_if_event_not_found(Guid userId)
        {
            Mock.Get(eventStore)
                .Setup(
                    x =>
                    x.LoadEvents<FakeUser>(userId, 0, CancellationToken.None))
                .ReturnsAsync(Enumerable.Empty<IDomainEvent>());

            FakeUser actual = await sut.Find(userId, CancellationToken.None);

            actual.Should().BeNull();
        }

        [Theory]
        [AutoData]
        public async Task Find_restores_aggregate_using_memento_if_found(
            FakeUser user,
            string username)
        {
            // Arrange
            var memento = user.SaveToMemento();
            user.ChangeUsername(username);

            Mock.Get(mementoStore)
                .Setup(x => x.Find<FakeUser>(user.Id, CancellationToken.None))
                .ReturnsAsync(memento);

            Mock.Get(eventStore)
                .Setup(
                    x =>
                    x.LoadEvents<FakeUser>(user.Id, 1, CancellationToken.None))
                .ReturnsAsync(user.PendingEvents.Skip(1))
                .Verifiable();

            // Act
            FakeUser actual = await sut.Find(user.Id, CancellationToken.None);

            // Assert
            Mock.Get(eventStore).Verify();
            actual.ShouldBeEquivalentTo(
                user, opts => opts.Excluding(x => x.PendingEvents));
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
