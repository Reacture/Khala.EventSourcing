namespace Khala.EventSourcing.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Khala.FakeDomain;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class SqlEventSourcedRepository_specs
    {
        [TestMethod]
        public void sut_implements_ISqlEventSourcedRepository()
        {
            typeof(SqlEventSourcedRepository<FakeUser>).Should().Implement<ISqlEventSourcedRepository<FakeUser>>();
        }

        [TestMethod]
        public void sut_implements_IEventSourcedRepositoryT()
        {
            typeof(SqlEventSourcedRepository<FakeUser>).Should().Implement<IEventSourcedRepository<FakeUser>>();
        }

        [TestMethod]
        public async Task SaveAndPublish_saves_events()
        {
            // Arrange
            var user = new FakeUser(id: Guid.NewGuid(), username: Guid.NewGuid().ToString());
            string operationId = Guid.NewGuid().ToString();
            var correlationId = Guid.NewGuid();
            string contributor = Guid.NewGuid().ToString();
            user.ChangeUsername(username: Guid.NewGuid().ToString());
            var pendingEvents = new List<IDomainEvent>(user.PendingEvents);

            ISqlEventStore eventStore = Mock.Of<ISqlEventStore>();

            var sut = new SqlEventSourcedRepository<FakeUser>(
                eventStore,
                Mock.Of<ISqlEventPublisher>(),
                FakeUser.Factory);

            // Act
            await sut.SaveAndPublish(user, operationId, correlationId, contributor);

            // Assert
            Mock.Get(eventStore).Verify(
                x =>
                x.SaveEvents<FakeUser>(pendingEvents, operationId, correlationId, contributor, default),
                Times.Once());
        }

        [TestMethod]
        public async Task SaveAndPublish_publishes_events()
        {
            // Arrange
            var user = new FakeUser(id: Guid.NewGuid(), username: Guid.NewGuid().ToString());
            string operationId = Guid.NewGuid().ToString();
            var correlationId = Guid.NewGuid();
            string contributor = Guid.NewGuid().ToString();
            user.ChangeUsername(username: Guid.NewGuid().ToString());

            ISqlEventPublisher eventPublisher = Mock.Of<ISqlEventPublisher>();

            var sut = new SqlEventSourcedRepository<FakeUser>(
                Mock.Of<ISqlEventStore>(),
                eventPublisher,
                FakeUser.Factory);

            // Act
            await sut.SaveAndPublish(user, operationId, correlationId, contributor);

            // Assert
            Mock.Get(eventPublisher).Verify(
                x =>
                x.FlushPendingEvents<FakeUser>(user.Id, default),
                Times.Once());
        }

        [TestMethod]
        public void SaveAndPublish_does_not_publish_events_if_fails_to_save_events()
        {
            // Arrange
            var user = new FakeUser(id: Guid.NewGuid(), username: Guid.NewGuid().ToString());
            string operationId = Guid.NewGuid().ToString();
            var correlationId = Guid.NewGuid();
            string contributor = Guid.NewGuid().ToString();
            user.ChangeUsername(username: Guid.NewGuid().ToString());

            ISqlEventStore eventStore = Mock.Of<ISqlEventStore>();
            Mock.Get(eventStore)
                .Setup(
                    x =>
                    x.SaveEvents<FakeUser>(
                        It.IsAny<IEnumerable<IDomainEvent>>(),
                        operationId,
                        correlationId,
                        contributor,
                        default))
                .Throws<InvalidOperationException>();

            ISqlEventPublisher eventPublisher = Mock.Of<ISqlEventPublisher>();

            var sut = new SqlEventSourcedRepository<FakeUser>(
                eventStore,
                eventPublisher,
                FakeUser.Factory);

            // Act
            Func<Task> action = () => sut.SaveAndPublish(user, operationId, correlationId, contributor);

            // Assert
            action.ShouldThrow<InvalidOperationException>();
            Mock.Get(eventPublisher).Verify(
                x =>
                x.FlushPendingEvents<FakeUser>(user.Id, It.IsAny<CancellationToken>()),
                Times.Never());
        }

        [TestMethod]
        public async Task SaveAndPublish_saves_memento()
        {
            // Arrange
            var user = new FakeUser(id: Guid.NewGuid(), username: Guid.NewGuid().ToString());
            string operationId = Guid.NewGuid().ToString();
            var correlationId = Guid.NewGuid();
            string contributor = Guid.NewGuid().ToString();

            IMementoStore mementoStore = Mock.Of<IMementoStore>();

            var sut = new SqlEventSourcedRepository<FakeUser>(
                Mock.Of<ISqlEventStore>(),
                Mock.Of<ISqlEventPublisher>(),
                mementoStore,
                FakeUser.Factory,
                FakeUser.Factory);

            // Act
            await sut.SaveAndPublish(user, operationId, correlationId, contributor);

            // Assert
            Mock.Get(mementoStore).Verify(
                x =>
                x.Save<FakeUser>(
                    user.Id,
                    It.Is<FakeUserMemento>(
                        p =>
                        p.Version == user.Version &&
                        p.Username == user.Username),
                    default),
                Times.Once());
        }

        [TestMethod]
        public void SaveAndPublish_does_not_saves_memento_if_fails_to_save_events()
        {
            // Arrange
            var user = new FakeUser(id: Guid.NewGuid(), username: Guid.NewGuid().ToString());
            string operationId = Guid.NewGuid().ToString();
            var correlationId = Guid.NewGuid();
            string contributor = Guid.NewGuid().ToString();

            ISqlEventStore eventStore = Mock.Of<ISqlEventStore>();
            IMementoStore mementoStore = Mock.Of<IMementoStore>();

            Mock.Get(eventStore)
                .Setup(
                    x =>
                    x.SaveEvents<FakeUser>(
                        It.IsAny<IEnumerable<IDomainEvent>>(),
                        operationId,
                        correlationId,
                        contributor,
                        default))
                .Throws<InvalidOperationException>();

            var sut = new SqlEventSourcedRepository<FakeUser>(
                eventStore,
                Mock.Of<ISqlEventPublisher>(),
                mementoStore,
                FakeUser.Factory,
                FakeUser.Factory);

            // Act
            Func<Task> action = () => sut.SaveAndPublish(user, operationId, correlationId, contributor);

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

        [TestMethod]
        public async Task Find_loads_events()
        {
            // Arrange
            var user = new FakeUser(id: Guid.NewGuid(), username: Guid.NewGuid().ToString());
            user.ChangeUsername(username: Guid.NewGuid().ToString());

            ISqlEventStore eventStore = Mock.Of<ISqlEventStore>();

            Mock.Get(eventStore)
                .Setup(x => x.LoadEvents<FakeUser>(user.Id, 0, default))
                .ReturnsAsync(user.FlushPendingEvents())
                .Verifiable();

            var sut = new SqlEventSourcedRepository<FakeUser>(
                eventStore,
                Mock.Of<ISqlEventPublisher>(),
                FakeUser.Factory);

            // Act
            await sut.Find(user.Id, default);

            // Assert
            Mock.Get(eventStore).Verify();
        }

        [TestMethod]
        public async Task Find_restores_aggregate_from_events()
        {
            // Arrange
            var user = new FakeUser(id: Guid.NewGuid(), username: Guid.NewGuid().ToString());
            user.ChangeUsername(username: Guid.NewGuid().ToString());

            ISqlEventStore eventStore = Mock.Of<ISqlEventStore>();

            Mock.Get(eventStore)
                .Setup(x => x.LoadEvents<FakeUser>(user.Id, 0, default))
                .ReturnsAsync(user.FlushPendingEvents())
                .Verifiable();

            var sut = new SqlEventSourcedRepository<FakeUser>(
                eventStore,
                Mock.Of<ISqlEventPublisher>(),
                FakeUser.Factory);

            // Act
            FakeUser actual = await sut.Find(user.Id, default);

            // Assert
            Mock.Get(eventStore).Verify();
            actual.ShouldBeEquivalentTo(user);
        }

        [TestMethod]
        public async Task Find_restores_aggregate_using_memento_if_found()
        {
            // Arrange
            var user = new FakeUser(id: Guid.NewGuid(), username: Guid.NewGuid().ToString());
            IMemento memento = user.SaveToMemento();
            user.ChangeUsername(username: Guid.NewGuid().ToString());

            IMementoStore mementoStore = Mock.Of<IMementoStore>();

            Mock.Get(mementoStore)
                .Setup(x => x.Find<FakeUser>(user.Id, default))
                .ReturnsAsync(memento);

            ISqlEventStore eventStore = Mock.Of<ISqlEventStore>();

            Mock.Get(eventStore)
                .Setup(x => x.LoadEvents<FakeUser>(user.Id, 1, default))
                .ReturnsAsync(user.FlushPendingEvents().Skip(1))
                .Verifiable();

            var sut = new SqlEventSourcedRepository<FakeUser>(
                eventStore,
                Mock.Of<ISqlEventPublisher>(),
                mementoStore,
                FakeUser.Factory,
                FakeUser.Factory);

            // Act
            FakeUser actual = await sut.Find(user.Id, default);

            // Assert
            Mock.Get(eventStore).Verify();
            actual.ShouldBeEquivalentTo(user);
        }

        [TestMethod]
        public async Task Find_returns_null_if_no_event()
        {
            // Arrange
            var userId = Guid.NewGuid();

            ISqlEventStore eventStore = Mock.Of<ISqlEventStore>();

            Mock.Get(eventStore)
                .Setup(x => x.LoadEvents<FakeUser>(userId, 0, default))
                .ReturnsAsync(new IDomainEvent[0]);

            var sut = new SqlEventSourcedRepository<FakeUser>(
                eventStore,
                Mock.Of<ISqlEventPublisher>(),
                FakeUser.Factory);

            // Act
            FakeUser actual = await sut.Find(userId, default);

            // Assert
            actual.Should().BeNull();
        }

        [TestMethod]
        public async Task FindIdByUniqueIndexedProperty_relays_to_event_store()
        {
            // Arrange
            string name = Guid.NewGuid().ToString();
            string value = Guid.NewGuid().ToString();
            Guid? expected = Guid.NewGuid();

            ISqlEventStore eventStore = Mock.Of<ISqlEventStore>();

            Mock.Get(eventStore)
                .Setup(x => x.FindIdByUniqueIndexedProperty<FakeUser>(name, value, default))
                .ReturnsAsync(expected);

            var sut = new SqlEventSourcedRepository<FakeUser>(
                eventStore,
                Mock.Of<ISqlEventPublisher>(),
                FakeUser.Factory);

            // Act
            Guid? actual = await sut.FindIdByUniqueIndexedProperty(name, value, default);

            // Assert
            actual.Should().Be(expected);
        }
    }
}
