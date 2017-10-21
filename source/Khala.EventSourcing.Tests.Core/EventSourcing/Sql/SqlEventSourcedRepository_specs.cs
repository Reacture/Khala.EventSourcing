namespace Khala.EventSourcing.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Khala.FakeDomain;
    using Moq;
    using Xunit;

    public class SqlEventSourcedRepository_specs
    {
        [Fact]
        public void sut_implements_ISqlEventSourcedRepository()
        {
            typeof(SqlEventSourcedRepository<FakeUser>).Should().Implement<ISqlEventSourcedRepository<FakeUser>>();
        }

        [Fact]
        public void sut_implements_IEventSourcedRepositoryT()
        {
            typeof(SqlEventSourcedRepository<FakeUser>).Should().Implement<IEventSourcedRepository<FakeUser>>();
        }

        [Fact]
        public async Task SaveAndPublish_saves_events()
        {
            // Arrange
            var user = new FakeUser(id: Guid.NewGuid(), username: Guid.NewGuid().ToString());
            var correlationId = Guid.NewGuid();
            user.ChangeUsername(username: Guid.NewGuid().ToString());
            var pendingEvents = new List<IDomainEvent>(user.PendingEvents);

            var eventStore = Mock.Of<ISqlEventStore>();

            var sut = new SqlEventSourcedRepository<FakeUser>(
                eventStore,
                Mock.Of<ISqlEventPublisher>(),
                FakeUser.Factory);

            // Act
            await sut.SaveAndPublish(user, correlationId, default);

            // Assert
            Mock.Get(eventStore).Verify(
                x =>
                x.SaveEvents<FakeUser>(pendingEvents, correlationId, default),
                Times.Once());
        }

        [Fact]
        public async Task SaveAndPublish_publishes_events()
        {
            // Arrange
            var user = new FakeUser(id: Guid.NewGuid(), username: Guid.NewGuid().ToString());
            var correlationId = Guid.NewGuid();
            user.ChangeUsername(username: Guid.NewGuid().ToString());

            var eventPublisher = Mock.Of<ISqlEventPublisher>();

            var sut = new SqlEventSourcedRepository<FakeUser>(
                Mock.Of<ISqlEventStore>(),
                eventPublisher,
                FakeUser.Factory);

            // Act
            await sut.SaveAndPublish(user, correlationId, default);

            // Assert
            Mock.Get(eventPublisher).Verify(
                x =>
                x.FlushPendingEvents(user.Id, default),
                Times.Once());
        }

        [Fact]
        public void SaveAndPublish_does_not_publish_events_if_fails_to_save_events()
        {
            // Arrange
            var user = new FakeUser(id: Guid.NewGuid(), username: Guid.NewGuid().ToString());
            var correlationId = Guid.NewGuid();
            user.ChangeUsername(username: Guid.NewGuid().ToString());

            var eventStore = Mock.Of<ISqlEventStore>();
            Mock.Get(eventStore)
                .Setup(
                    x =>
                    x.SaveEvents<FakeUser>(
                        It.IsAny<IEnumerable<IDomainEvent>>(),
                        correlationId,
                        default))
                .Throws<InvalidOperationException>();

            var eventPublisher = Mock.Of<ISqlEventPublisher>();

            var sut = new SqlEventSourcedRepository<FakeUser>(
                eventStore,
                eventPublisher,
                FakeUser.Factory);

            // Act
            Func<Task> action = () => sut.SaveAndPublish(user, correlationId, default);

            // Assert
            action.ShouldThrow<InvalidOperationException>();
            Mock.Get(eventPublisher).Verify(
                x =>
                x.FlushPendingEvents(user.Id, It.IsAny<CancellationToken>()),
                Times.Never());
        }

        [Fact]
        public async Task SaveAndPublish_saves_memento()
        {
            // Arrange
            var user = new FakeUser(id: Guid.NewGuid(), username: Guid.NewGuid().ToString());
            var correlationId = Guid.NewGuid();

            var mementoStore = Mock.Of<IMementoStore>();

            var sut = new SqlEventSourcedRepository<FakeUser>(
                Mock.Of<ISqlEventStore>(),
                Mock.Of<ISqlEventPublisher>(),
                mementoStore,
                FakeUser.Factory,
                FakeUser.Factory);

            // Act
            await sut.SaveAndPublish(user, correlationId, default);

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

        [Fact]
        public void SaveAndPublish_does_not_saves_memento_if_fails_to_save_events()
        {
            // Arrange
            var user = new FakeUser(id: Guid.NewGuid(), username: Guid.NewGuid().ToString());
            var correlationId = Guid.NewGuid();

            var eventStore = Mock.Of<ISqlEventStore>();
            var mementoStore = Mock.Of<IMementoStore>();

            Mock.Get(eventStore)
                .Setup(
                    x =>
                    x.SaveEvents<FakeUser>(
                        It.IsAny<IEnumerable<IDomainEvent>>(),
                        correlationId,
                        default))
                .Throws<InvalidOperationException>();

            var sut = new SqlEventSourcedRepository<FakeUser>(
                eventStore,
                Mock.Of<ISqlEventPublisher>(),
                mementoStore,
                FakeUser.Factory,
                FakeUser.Factory);

            // Act
            Func<Task> action = () => sut.SaveAndPublish(user, correlationId, default);

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

        [Fact]
        public async Task Find_loads_events()
        {
            // Arrange
            var user = new FakeUser(id: Guid.NewGuid(), username: Guid.NewGuid().ToString());
            user.ChangeUsername(username: Guid.NewGuid().ToString());

            var eventStore = Mock.Of<ISqlEventStore>();

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

        [Fact]
        public async Task Find_restores_aggregate_from_events()
        {
            // Arrange
            var user = new FakeUser(id: Guid.NewGuid(), username: Guid.NewGuid().ToString());
            user.ChangeUsername(username: Guid.NewGuid().ToString());

            var eventStore = Mock.Of<ISqlEventStore>();

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

        [Fact]
        public async Task Find_restores_aggregate_using_memento_if_found()
        {
            // Arrange
            var user = new FakeUser(id: Guid.NewGuid(), username: Guid.NewGuid().ToString());
            IMemento memento = user.SaveToMemento();
            user.ChangeUsername(username: Guid.NewGuid().ToString());

            var mementoStore = Mock.Of<IMementoStore>();

            Mock.Get(mementoStore)
                .Setup(x => x.Find<FakeUser>(user.Id, default))
                .ReturnsAsync(memento);

            var eventStore = Mock.Of<ISqlEventStore>();

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

        [Fact]
        public async Task Find_returns_null_if_no_event()
        {
            // Arrange
            var userId = Guid.NewGuid();

            var eventStore = Mock.Of<ISqlEventStore>();

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

        [Fact]
        public async Task FindIdByUniqueIndexedProperty_relays_to_event_store()
        {
            // Arrange
            string name = Guid.NewGuid().ToString();
            string value = Guid.NewGuid().ToString();
            Guid? expected = Guid.NewGuid();

            var eventStore = Mock.Of<ISqlEventStore>();

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
