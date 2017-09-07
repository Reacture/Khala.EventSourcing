namespace Khala.EventSourcing.Azure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Khala.FakeDomain;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Storage;
    using Moq;
    using Ploeh.AutoFixture;
    using Ploeh.AutoFixture.AutoMoq;
    using Ploeh.AutoFixture.Idioms;

    [TestClass]
    public class AzureEventSourcedRepository_specs
    {
        private IFixture _fixture;
        private IAzureEventStore _eventStore;
        private IAzureEventPublisher _eventPublisher;
        private IMementoStore _mementoStore;
        private AzureEventSourcedRepository<FakeUser> _sut;

        [TestInitialize]
        public void TestInitialize()
        {
            _fixture = new Fixture().Customize(new AutoMoqCustomization());
            _eventStore = Mock.Of<IAzureEventStore>();
            _eventPublisher = Mock.Of<IAzureEventPublisher>();
            _mementoStore = Mock.Of<IMementoStore>();
            _sut = new AzureEventSourcedRepository<FakeUser>(
                _eventStore,
                _eventPublisher,
                _mementoStore,
                FakeUser.Factory,
                FakeUser.Factory);
        }

        [TestMethod]
        public void sut_implements_IEventSourcedRepositoryT()
        {
            _sut.Should().BeAssignableTo<IEventSourcedRepository<FakeUser>>();
        }

        [TestMethod]
        public void class_has_guard_clauses()
        {
            var assertion = new GuardClauseAssertion(_fixture);
            _fixture.Inject(CloudStorageAccount.DevelopmentStorageAccount.CreateCloudTableClient().GetTableReference("foo"));
            assertion.Verify(typeof(AzureEventSourcedRepository<>));
        }

        [TestMethod]
        public async Task Save_saves_events()
        {
            // Arrange
            var user = _fixture.Create<FakeUser>();
            var correlationId = Guid.NewGuid();
            user.ChangeUsername("foo");

            // Act
            await _sut.Save(user, correlationId, CancellationToken.None);

            // Assert
            Mock.Get(_eventStore).Verify(
                x =>
                x.SaveEvents<FakeUser>(
                    user.PendingEvents,
                    correlationId,
                    CancellationToken.None),
                Times.Once());
        }

        [TestMethod]
        public async Task Save_publishes_events()
        {
            var user = _fixture.Create<FakeUser>();
            var correlationId = Guid.NewGuid();
            user.ChangeUsername("foo");

            await _sut.Save(user, correlationId, CancellationToken.None);

            Mock.Get(_eventPublisher).Verify(
                x =>
                x.PublishPendingEvents<FakeUser>(
                    user.Id,
                    CancellationToken.None),
                Times.Once());
        }

        [TestMethod]
        public void Save_does_not_publish_events_if_fails_to_save()
        {
            // Arrange
            var user = _fixture.Create<FakeUser>();
            var correlationId = Guid.NewGuid();
            user.ChangeUsername("foo");
            Mock.Get(_eventStore)
                .Setup(
                    x =>
                    x.SaveEvents<FakeUser>(
                        It.IsAny<IEnumerable<IDomainEvent>>(),
                        It.IsAny<Guid?>(),
                        CancellationToken.None))
                .Throws<InvalidOperationException>();

            // Act
            Func<Task> action = () => _sut.Save(user, correlationId, CancellationToken.None);

            // Assert
            action.ShouldThrow<InvalidOperationException>();
            Mock.Get(_eventPublisher).Verify(
                x =>
                x.PublishPendingEvents<FakeUser>(
                    user.Id,
                    CancellationToken.None),
                Times.Never());
        }

        [TestMethod]
        public async Task Save_saves_memento()
        {
            var user = _fixture.Create<FakeUser>();
            var correlationId = Guid.NewGuid();
            user.ChangeUsername("foo");

            await _sut.Save(user, correlationId, CancellationToken.None);

            Mock.Get(_mementoStore).Verify(
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

        [TestMethod]
        public void Save_does_not_save_memento_if_fails_to_save_events()
        {
            // Arrange
            var user = _fixture.Create<FakeUser>();
            var correlationId = Guid.NewGuid();
            user.ChangeUsername("foo");
            Mock.Get(_eventStore)
                .Setup(
                    x =>
                    x.SaveEvents<FakeUser>(
                        It.IsAny<IEnumerable<IDomainEvent>>(),
                        It.IsAny<Guid?>(),
                        CancellationToken.None))
                .Throws<InvalidOperationException>();

            // Act
            Func<Task> action = () => _sut.Save(user, correlationId, CancellationToken.None);

            // Assert
            action.ShouldThrow<InvalidOperationException>();
            Mock.Get(_mementoStore).Verify(
                x =>
                x.Save<FakeUser>(
                        user.Id,
                        It.IsAny<IMemento>(),
                        It.IsAny<CancellationToken>()),
                Times.Never());
        }

        [TestMethod]
        public async Task Find_publishes_pending_events()
        {
            var user = _fixture.Create<FakeUser>();
            user.ChangeUsername("foo");

            await _sut.Find(user.Id, CancellationToken.None);

            Mock.Get(_eventPublisher).Verify(
                x =>
                x.PublishPendingEvents<FakeUser>(
                    user.Id,
                    CancellationToken.None),
                Times.Once());
        }

        [TestMethod]
        public async Task Find_restores_aggregate()
        {
            var user = _fixture.Create<FakeUser>();
            user.ChangeUsername("foo");
            Mock.Get(_eventStore)
                .Setup(x => x.LoadEvents<FakeUser>(user.Id, 0, CancellationToken.None))
                .ReturnsAsync(user.PendingEvents);

            FakeUser actual = await _sut.Find(user.Id, CancellationToken.None);

            actual.ShouldBeEquivalentTo(user, opts => opts.Excluding(x => x.PendingEvents));
        }

        [TestMethod]
        public void Find_does_not_load_events_if_fails_to_publish_events()
        {
            // Arrange
            var user = _fixture.Create<FakeUser>();
            user.ChangeUsername("foo");
            Mock.Get(_eventPublisher)
                .Setup(
                    x =>
                    x.PublishPendingEvents<FakeUser>(
                        user.Id,
                        CancellationToken.None))
                .Throws<InvalidOperationException>();

            // Act
            Func<Task> action = () => _sut.Find(user.Id, CancellationToken.None);

            // Assert
            action.ShouldThrow<InvalidOperationException>();
            Mock.Get(_eventStore).Verify(
                x =>
                x.LoadEvents<FakeUser>(
                    user.Id,
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()),
                Times.Never());
        }

        [TestMethod]
        public async Task Find_returns_null_if_event_not_found()
        {
            var userId = Guid.NewGuid();
            Mock.Get(_eventStore)
                .Setup(
                    x =>
                    x.LoadEvents<FakeUser>(userId, 0, CancellationToken.None))
                .ReturnsAsync(Enumerable.Empty<IDomainEvent>());

            FakeUser actual = await _sut.Find(userId, CancellationToken.None);

            actual.Should().BeNull();
        }

        [TestMethod]
        public async Task Find_restores_aggregate_using_memento_if_found()
        {
            // Arrange
            var user = _fixture.Create<FakeUser>();
            var memento = user.SaveToMemento();
            user.ChangeUsername("foo");

            Mock.Get(_mementoStore)
                .Setup(x => x.Find<FakeUser>(user.Id, CancellationToken.None))
                .ReturnsAsync(memento);

            Mock.Get(_eventStore)
                .Setup(
                    x =>
                    x.LoadEvents<FakeUser>(user.Id, 1, CancellationToken.None))
                .ReturnsAsync(user.PendingEvents.Skip(1))
                .Verifiable();

            // Act
            FakeUser actual = await _sut.Find(user.Id, CancellationToken.None);

            // Assert
            Mock.Get(_eventStore).Verify();
            actual.ShouldBeEquivalentTo(
                user, opts => opts.Excluding(x => x.PendingEvents));
        }
    }
}
