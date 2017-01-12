using System;
using System.Collections.Generic;
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
using Xunit;

namespace ReactiveArchitecture.EventSourcing.Azure
{
    public class AzureEventSourcedRepository_features
    {
        public interface IFactory<T>
        {
            T Func(Guid sourceId, IEnumerable<IDomainEvent> pastEvents);
        }

        private IFixture fixture;
        private IAzureEventStore eventStore;
        private IAzureEventPublisher eventPublisher;
        private IAzureEventCorrector eventCorrector;
        private IFactory<FakeUser> factory;
        private AzureEventSourcedRepository<FakeUser> sut;

        public AzureEventSourcedRepository_features()
        {
            fixture = new Fixture().Customize(new AutoMoqCustomization());
            eventStore = Mock.Of<IAzureEventStore>();
            eventPublisher = Mock.Of<IAzureEventPublisher>();
            eventCorrector = Mock.Of<IAzureEventCorrector>();
            factory = Mock.Of<IFactory<FakeUser>>();
            sut = new AzureEventSourcedRepository<FakeUser>(
                eventStore,
                eventPublisher,
                eventCorrector,
                factory.Func);
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
            assertion.Verify(typeof(AzureEventSourcedRepository<>));
        }

        [Theory]
        [AutoData]
        public async Task Save_saves_events(
            Guid userId,
            FakeUserCreated userCreated,
            FakeUsernameChanged userNameChanged)
        {
            var events = new DomainEvent[] { userCreated, userNameChanged };
            RaiseEvents(userId, events);
            var source = new FakeUser
            {
                Id = userId,
                Version = events.Last().Version,
                PendingEvents = events
            };

            await sut.Save(source);

            Mock.Get(eventStore).Verify(x => x.SaveEvents<FakeUser>(events), Times.Once());
        }

        [Theory]
        [AutoData]
        public async Task Save_publishes_events(
            Guid userId,
            FakeUserCreated userCreated,
            FakeUsernameChanged userNameChanged)
        {
            var events = new DomainEvent[] { userCreated, userNameChanged };
            RaiseEvents(userId, events);
            var source = new FakeUser
            {
                Id = userId,
                Version = events.Last().Version,
                PendingEvents = events
            };

            await sut.Save(source);

            Mock.Get(eventPublisher).Verify(x => x.PublishPendingEvents<FakeUser>(userId), Times.Once());
        }

        [Theory]
        [AutoData]
        public void Save_does_not_publish_events_if_fails_to_save(
            Guid userId,
            FakeUserCreated userCreated,
            FakeUsernameChanged userNameChanged)
        {
            // Arrange
            var events = new DomainEvent[] { userCreated, userNameChanged };
            RaiseEvents(userId, events);
            var source = new FakeUser
            {
                Id = userId,
                Version = events.Last().Version,
                PendingEvents = events
            };

            Mock.Get(eventStore)
                .Setup(x => x.SaveEvents<FakeUser>(It.IsAny<IEnumerable<IDomainEvent>>()))
                .Throws<InvalidOperationException>();

            // Act
            Func<Task> action = () => sut.Save(source);

            // Assert
            action.ShouldThrow<InvalidOperationException>();
            Mock.Get(eventPublisher).Verify(x => x.PublishPendingEvents<FakeUser>(userId), Times.Never());
        }

        [Theory]
        [AutoData]
        public async Task Find_corrects_damaged_events(
            Guid userId,
            FakeUserCreated userCreated,
            FakeUsernameChanged userNameChanged)
        {
            var events = new DomainEvent[] { userCreated, userNameChanged };
            RaiseEvents(userId, events);
            var source = new FakeUser
            {
                Id = userId,
                Version = events.Last().Version,
                PendingEvents = events
            };

            await sut.Find(userId);

            Mock.Get(eventCorrector).Verify(x => x.CorrectEvents<FakeUser>(userId), Times.Once());
        }

        [Theory]
        [AutoData]
        public async Task Find_restores_aggregate(
            Guid userId,
            FakeUserCreated userCreated,
            FakeUsernameChanged userNameChanged)
        {
            // Arrange
            var events = new DomainEvent[] { userCreated, userNameChanged };
            RaiseEvents(userId, events);
            var source = new FakeUser
            {
                Id = userId,
                Version = events.Last().Version,
                PendingEvents = events
            };

            Mock.Get(eventStore)
                .Setup(x => x.LoadEvents<FakeUser>(userId, 0))
                .ReturnsAsync(source.PendingEvents);

            Mock.Get(factory)
                .Setup(x => x.Func(userId, source.PendingEvents))
                .Returns(source);

            // Act
            FakeUser actual = await sut.Find(userId);

            // Assert
            actual.Should().BeSameAs(source);
        }

        [Theory]
        [AutoData]
        public void Find_does_not_load_events_if_fails_to_correct_events(
            Guid userId,
            FakeUserCreated userCreated,
            FakeUsernameChanged userNameChanged)
        {
            // Arrange
            var events = new DomainEvent[] { userCreated, userNameChanged };
            RaiseEvents(userId, events);
            var source = new FakeUser
            {
                Id = userId,
                Version = events.Last().Version,
                PendingEvents = events
            };

            Mock.Get(eventCorrector)
                .Setup(x => x.CorrectEvents<FakeUser>(userId))
                .Throws<InvalidOperationException>();

            // Act
            Func<Task> action = () => sut.Find(userId);

            // Assert
            action.ShouldThrow<InvalidOperationException>();
            Mock.Get(eventStore).Verify(x => x.LoadEvents<FakeUser>(userId, 0), Times.Never());
        }

        [Theory]
        [AutoData]
        public async Task Find_returns_null_if_event_not_found(Guid userId)
        {
            Mock.Get(eventStore)
                .Setup(x => x.LoadEvents<FakeUser>(userId, 0))
                .ReturnsAsync(Enumerable.Empty<IDomainEvent>());

            FakeUser actual = await sut.Find(userId);

            actual.Should().BeNull();
            Mock.Get(factory).Verify(x => x.Func(userId, It.IsAny<IEnumerable<IDomainEvent>>()), Times.Never());
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
