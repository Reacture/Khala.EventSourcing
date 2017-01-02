using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Ploeh.AutoFixture;
using Ploeh.AutoFixture.AutoMoq;
using Ploeh.AutoFixture.Idioms;

namespace ReactiveArchitecture.EventSourcing
{
    [TestClass]
    public class EventSourcedRepository_features
    {
        public interface IFakeEventSourced : IEventSourced
        {
        }

        public interface IFactory
        {
            IFakeEventSourced Func(
                Guid sourceId,
                IEnumerable<IDomainEvent> domainEvents);
        }

        private IFixture fixture;
        private IEventStore eventStore;
        private IEventPublisher eventPublisher;
        private IFactory factory;
        private EventSourcedRepository<IFakeEventSourced> sut;

        [TestInitialize]
        public void TestInitialize()
        {
            fixture = new Fixture().Customize(new AutoMoqCustomization());
            eventStore = Mock.Of<IEventStore>();
            eventPublisher = Mock.Of<IEventPublisher>();
            factory = Mock.Of<IFactory>();
            sut = new EventSourcedRepository<IFakeEventSourced>(
                eventStore, eventPublisher, factory.Func);
        }

        [TestMethod]
        public void sut_implements_IEventSourcedRepositoryT()
        {
            sut.Should().BeAssignableTo
                <IEventSourcedRepository<IFakeEventSourced>>();
        }

        [TestMethod]
        public void class_has_guard_clauses()
        {
            var assertion = new GuardClauseAssertion(fixture);
            assertion.Verify(typeof(EventSourcedRepository<>));
        }

        [TestMethod]
        public async Task Save_saves_events()
        {
            var events = fixture.CreateMany<IDomainEvent>();
            var source = Mock.Of<IFakeEventSourced>(
                x => x.PendingEvents == events);

            await sut.Save(source);

            Mock.Get(eventStore).Verify(
                x => x.SaveEvents<IFakeEventSourced>(events), Times.Once());
        }

        [TestMethod]
        public async Task Save_publishes_events()
        {
            var sourceId = Guid.NewGuid();
            var source = Mock.Of<IFakeEventSourced>(x => x.Id == sourceId);

            await sut.Save(source);

            Mock.Get(eventPublisher).Verify(
                x => x.PublishPendingEvents<IFakeEventSourced>(sourceId),
                Times.Once());
        }

        [TestMethod]
        public void Save_does_not_publish_events_if_fails_to_save_events()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var events = fixture.CreateMany<IDomainEvent>();
            var source = Mock.Of<IFakeEventSourced>(
                x =>
                x.Id == sourceId &&
                x.PendingEvents == events);

            Mock.Get(eventStore)
                .Setup(x => x.SaveEvents<IFakeEventSourced>(events))
                .Throws<InvalidOperationException>();

            // Act
            Func<Task> action = () => sut.Save(source);

            // Assert
            action.ShouldThrow<InvalidOperationException>();
            Mock.Get(eventPublisher).Verify(
                x => x.PublishPendingEvents<IFakeEventSourced>(sourceId),
                Times.Never());
        }

        [TestMethod]
        public async Task Find_loads_events()
        {
            var sourceId = Guid.NewGuid();
            var events = fixture.CreateMany<IDomainEvent>();
            Mock.Get(eventStore)
                .Setup(x => x.LoadEvents<IFakeEventSourced>(sourceId, 0))
                .ReturnsAsync(events)
                .Verifiable();

            await sut.Find(sourceId);

            Mock.Get(eventStore).Verify();
        }

        [TestMethod]
        public async Task Find_restores_aggregate_from_events()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var source = Mock.Of<IFakeEventSourced>();
            var events = fixture.CreateMany<IDomainEvent>();

            Mock.Get(eventStore)
                .Setup(x => x.LoadEvents<IFakeEventSourced>(sourceId, 0))
                .ReturnsAsync(events);

            Mock.Get(factory)
                .Setup(
                    x =>
                    x.Func(
                        sourceId,
                        It.Is<IEnumerable<IDomainEvent>>(
                            p =>
                            p.SequenceEqual(events))))
                .Returns(source)
                .Verifiable();

            // Act
            IFakeEventSourced actual = await sut.Find(sourceId);

            // Assert
            Mock.Get(factory).Verify();
            actual.Should().BeSameAs(source);
        }

        [TestMethod]
        public async Task Find_returns_null_if_no_event()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var source = Mock.Of<IFakeEventSourced>();
            var events = new IDomainEvent[0];

            Mock.Get(eventStore)
                .Setup(x => x.LoadEvents<IFakeEventSourced>(sourceId, 0))
                .ReturnsAsync(events);

            Mock.Get(factory)
                .Setup(x => x.Func(sourceId, events))
                .Returns(source);

            // Act
            IFakeEventSourced actual = await sut.Find(sourceId);

            // Assert
            Mock.Get(factory).Verify(
                x => x.Func(sourceId, It.IsAny<IEnumerable<IDomainEvent>>()),
                Times.Never());
            actual.Should().BeNull();
        }
    }
}
