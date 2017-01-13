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
using ReactiveArchitecture.FakeDomain;

namespace ReactiveArchitecture.EventSourcing.Sql
{
    [TestClass]
    public class SqlEventSourcedRepository_features
    {
        private IFixture fixture;
        private ISqlEventStore eventStore;
        private ISqlEventPublisher eventPublisher;
        private IMementoStore mementoStore;
        private SqlEventSourcedRepository<FakeUser> sut;

        [TestInitialize]
        public void TestInitialize()
        {
            fixture = new Fixture().Customize(new AutoMoqCustomization());
            eventStore = Mock.Of<ISqlEventStore>();
            eventPublisher = Mock.Of<ISqlEventPublisher>();
            mementoStore = Mock.Of<IMementoStore>();
            sut = new SqlEventSourcedRepository<FakeUser>(
                eventStore,
                eventPublisher,
                mementoStore,
                FakeUser.Factory,
                FakeUser.Factory);
        }

        [TestMethod]
        public void sut_implements_ISqlEventSourcedRepository()
        {
            sut.Should().BeAssignableTo
                <ISqlEventSourcedRepository<FakeUser>>();
        }

        [TestMethod]
        public void sut_implements_IEventSourcedRepositoryT()
        {
            sut.Should().BeAssignableTo
                <IEventSourcedRepository<FakeUser>>();
        }

        [TestMethod]
        public void class_has_guard_clauses()
        {
            var assertion = new GuardClauseAssertion(fixture);
            assertion.Verify(typeof(SqlEventSourcedRepository<>));
        }

        [TestMethod]
        public async Task Save_saves_events()
        {
            var user = fixture.Create<FakeUser>();
            user.ChangeUsername(fixture.Create("username"));

            await sut.Save(user);

            Mock.Get(eventStore).Verify(
                x => x.SaveEvents<FakeUser>(user.PendingEvents), Times.Once());
        }

        [TestMethod]
        public async Task Save_publishes_events()
        {
            var user = fixture.Create<FakeUser>();
            user.ChangeUsername(fixture.Create("username"));

            await sut.Save(user);

            Mock.Get(eventPublisher).Verify(
                x => x.PublishPendingEvents<FakeUser>(user.Id),
                Times.Once());
        }

        [TestMethod]
        public void Save_does_not_publish_events_if_fails_to_save_events()
        {
            var user = fixture.Create<FakeUser>();
            user.ChangeUsername(fixture.Create("username"));
            Mock.Get(eventStore)
                .Setup(x => x.SaveEvents<FakeUser>(user.PendingEvents))
                .Throws<InvalidOperationException>();

            Func<Task> action = () => sut.Save(user);

            action.ShouldThrow<InvalidOperationException>();
            Mock.Get(eventPublisher).Verify(
                x => x.PublishPendingEvents<FakeUser>(user.Id),
                Times.Never());
        }

        [TestMethod]
        public async Task Save_saves_memento()
        {
            var user = fixture.Create<FakeUser>();

            await sut.Save(user);

            Mock.Get(mementoStore).Verify(
                x =>
                x.Save<FakeUser>(
                    user.Id,
                    It.Is<FakeUserMemento>(
                        p =>
                        p.Version == user.Version &&
                        p.Username == user.Username)),
                Times.Once());
        }

        [TestMethod]
        public void Save_does_not_saves_memento_if_fails_to_save_events()
        {
            var user = fixture.Create<FakeUser>();
            Mock.Get(eventStore)
                .Setup(x => x.SaveEvents<FakeUser>(user.PendingEvents))
                .Throws<InvalidOperationException>();

            Func<Task> action = () => sut.Save(user);

            action.ShouldThrow<InvalidOperationException>();
            Mock.Get(mementoStore).Verify(
                x => x.Save<FakeUser>(user.Id, It.IsAny<IMemento>()),
                Times.Never());
        }

        [TestMethod]
        public async Task Find_loads_events()
        {
            var user = fixture.Create<FakeUser>();
            user.ChangeUsername(fixture.Create("username"));
            Mock.Get(eventStore)
                .Setup(x => x.LoadEvents<FakeUser>(user.Id, 0))
                .ReturnsAsync(user.PendingEvents)
                .Verifiable();

            await sut.Find(user.Id);

            Mock.Get(eventStore).Verify();
        }

        [TestMethod]
        public async Task Find_restores_aggregate_from_events()
        {
            // Arrange
            var user = fixture.Create<FakeUser>();
            user.ChangeUsername(fixture.Create("username"));

            Mock.Get(eventStore)
                .Setup(x => x.LoadEvents<FakeUser>(user.Id, 0))
                .ReturnsAsync(user.PendingEvents)
                .Verifiable();

            // Act
            FakeUser actual = await sut.Find(user.Id);

            // Assert
            Mock.Get(eventStore).Verify();
            actual.ShouldBeEquivalentTo(
                user, opts => opts.Excluding(x => x.PendingEvents));
        }

        [TestMethod]
        public async Task Find_restores_aggregate_using_memento_if_found()
        {
            // Arrange
            var user = fixture.Create<FakeUser>();
            IMemento memento = user.SaveToMemento();
            user.ChangeUsername(fixture.Create("username"));

            Mock.Get(mementoStore)
                .Setup(x => x.Find<FakeUser>(user.Id))
                .ReturnsAsync(memento);

            Mock.Get(eventStore)
                .Setup(x => x.LoadEvents<FakeUser>(user.Id, 1))
                .ReturnsAsync(user.PendingEvents.Skip(1))
                .Verifiable();

            // Act
            FakeUser actual = await sut.Find(user.Id);

            // Assert
            Mock.Get(eventStore).Verify();
            actual.ShouldBeEquivalentTo(
                user, opts => opts.Excluding(x => x.PendingEvents));
        }

        [TestMethod]
        public async Task Find_returns_null_if_no_event()
        {
            var userId = Guid.NewGuid();
            Mock.Get(eventStore)
                .Setup(x => x.LoadEvents<FakeUser>(userId, 0))
                .ReturnsAsync(new IDomainEvent[0]);

            FakeUser actual = await sut.Find(userId);

            actual.Should().BeNull();
        }

        [TestMethod]
        public async Task FindIdByUniqueIndexedProperty_relays_to_event_store()
        {
            // Arrange
            var name = fixture.Create<string>();
            var value = fixture.Create<string>();
            var expected = fixture.Create<Guid?>();

            var eventStore = Mock.Of<ISqlEventStore>(
                x =>
                x.FindIdByUniqueIndexedProperty<FakeUser>(name, value) == Task.FromResult(expected));

            var sut = new SqlEventSourcedRepository<FakeUser>(
                eventStore,
                Mock.Of<ISqlEventPublisher>(),
                Mock.Of<Func<Guid, IEnumerable<IDomainEvent>, FakeUser>>());

            // Act
            Guid? actual = await sut.FindIdByUniqueIndexedProperty(name, value);

            // Assert
            actual.Should().Be(expected);
        }
    }
}
