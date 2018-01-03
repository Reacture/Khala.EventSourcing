namespace Khala.EventSourcing.Azure
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Khala.FakeDomain;
    using Khala.Messaging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.RetryPolicies;
    using Microsoft.WindowsAzure.Storage.Table;
    using Moq;
    using Ploeh.AutoFixture;
    using Ploeh.AutoFixture.AutoMoq;

    [TestClass]
    public class AzureEventSourcedRepository_specs
    {
        private IFixture _fixture;
        private IAzureEventStore _eventStore;
        private IAzureEventPublisher _eventPublisher;
        private IMementoStore _mementoStore;
        private AzureEventSourcedRepository<FakeUser> _sut;

        public TestContext TestContext { get; set; }

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
        public void constructor_sets_EventPublisher_correctly()
        {
            var eventPublisher = Mock.Of<IAzureEventPublisher>();

            var sut = new AzureEventSourcedRepository<FakeUser>(
                Mock.Of<IAzureEventStore>(),
                eventPublisher,
                FakeUser.Factory);

            sut.EventPublisher.Should().BeSameAs(eventPublisher);
        }

        [TestMethod]
        public async Task SaveAndPublish_saves_events()
        {
            // Arrange
            var user = _fixture.Create<FakeUser>();
            var operationId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            string contributor = Guid.NewGuid().ToString();
            user.ChangeUsername("foo");
            var pendingEvents = new List<IDomainEvent>(user.PendingEvents);

            // Act
            await _sut.SaveAndPublish(user, operationId, correlationId, contributor);

            // Assert
            Mock.Get(_eventStore).Verify(
                x =>
                x.SaveEvents<FakeUser>(
                    pendingEvents,
                    correlationId,
                    contributor,
                    CancellationToken.None),
                Times.Once());
        }

        [TestMethod]
        public async Task SaveAndPublish_publishes_events()
        {
            var user = _fixture.Create<FakeUser>();
            var operationId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            string contributor = Guid.NewGuid().ToString();
            user.ChangeUsername("foo");

            await _sut.SaveAndPublish(user, operationId, correlationId, contributor);

            Mock.Get(_eventPublisher).Verify(
                x =>
                x.FlushPendingEvents<FakeUser>(
                    user.Id,
                    CancellationToken.None),
                Times.Once());
        }

        [TestMethod]
        public void SaveAndPublish_does_not_publish_events_if_fails_to_save()
        {
            // Arrange
            var user = _fixture.Create<FakeUser>();
            var operationId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            string contributor = Guid.NewGuid().ToString();
            user.ChangeUsername("foo");
            Mock.Get(_eventStore)
                .Setup(
                    x =>
                    x.SaveEvents<FakeUser>(
                        It.IsAny<IEnumerable<IDomainEvent>>(),
                        It.IsAny<Guid?>(),
                        It.IsAny<string>(),
                        CancellationToken.None))
                .Throws<InvalidOperationException>();

            // Act
            Func<Task> action = () => _sut.SaveAndPublish(user, operationId, correlationId, contributor);

            // Assert
            action.ShouldThrow<InvalidOperationException>();
            Mock.Get(_eventPublisher).Verify(
                x =>
                x.FlushPendingEvents<FakeUser>(
                    user.Id,
                    CancellationToken.None),
                Times.Never());
        }

        [TestMethod]
        public async Task SaveAndPublish_saves_memento()
        {
            var user = _fixture.Create<FakeUser>();
            var operationId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            string contributor = Guid.NewGuid().ToString();
            user.ChangeUsername("foo");

            await _sut.SaveAndPublish(user, operationId, correlationId, contributor);

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
        public void SaveAndPublish_does_not_save_memento_if_fails_to_save_events()
        {
            // Arrange
            var user = _fixture.Create<FakeUser>();
            var operationId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            string contributor = Guid.NewGuid().ToString();
            user.ChangeUsername("foo");
            Mock.Get(_eventStore)
                .Setup(
                    x =>
                    x.SaveEvents<FakeUser>(
                        It.IsAny<IEnumerable<IDomainEvent>>(),
                        It.IsAny<Guid?>(),
                        It.IsAny<string>(),
                        CancellationToken.None))
                .Throws<InvalidOperationException>();

            // Act
            Func<Task> action = () => _sut.SaveAndPublish(user, operationId, correlationId, contributor);

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
                x.FlushPendingEvents<FakeUser>(
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
                .ReturnsAsync(user.FlushPendingEvents());

            FakeUser actual = await _sut.Find(user.Id, CancellationToken.None);

            actual.ShouldBeEquivalentTo(user);
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
                    x.FlushPendingEvents<FakeUser>(
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
                .ReturnsAsync(user.FlushPendingEvents().Skip(1))
                .Verifiable();

            // Act
            FakeUser actual = await _sut.Find(user.Id, CancellationToken.None);

            // Assert
            Mock.Get(_eventStore).Verify();
            actual.ShouldBeEquivalentTo(user);
        }

        [TestMethod]
        public async Task SaveAndPublish_is_concurrency_safe()
        {
            // Arrange
            CloudTable eventTable = InitializeEventTable("AzureEventSourcedRepositoryConcurrencyTest");
            var serializer = new JsonMessageSerializer();
            var messageBus = new MessageBus();
            var eventStore = new AzureEventStore(eventTable, serializer);
            var eventPublisher = new AzureEventPublisher(eventTable, serializer, messageBus);
            var sut = new AzureEventSourcedRepository<FakeUser>(eventStore, eventPublisher, FakeUser.Factory);
            var userId = Guid.NewGuid();
            await sut.SaveAndPublish(new FakeUser(userId, Guid.NewGuid().ToString()));

            // Act
            async Task Process()
            {
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        FakeUser user = await sut.Find(userId);
                        user.ChangeUsername(Guid.NewGuid().ToString());
                        user.ChangeUsername(Guid.NewGuid().ToString());
                        user.ChangeUsername(Guid.NewGuid().ToString());
                        await sut.SaveAndPublish(user);
                    }
                    catch
                    {
                    }
                }
            }

            await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Process()));
            await Task.Delay(millisecondsDelay: 100);

            // Assert
            IEnumerable<IDomainEvent> expected = await eventStore.LoadEvents<FakeUser>(userId);
            List<IDomainEvent> actual = messageBus.Log.Select(e => (IDomainEvent)e.Message).Distinct(e => e.Version).OrderBy(e => e.Version).ToList();
            actual.ShouldAllBeEquivalentTo(expected, opts => opts.WithStrictOrdering().Excluding(e => e.RaisedAt));
        }

        private CloudTable InitializeEventTable(string eventTableName)
        {
            CloudTable eventTable = default;

            try
            {
                var storageAccount = CloudStorageAccount.DevelopmentStorageAccount;
                CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
                eventTable = tableClient.GetTableReference(eventTableName);
                eventTable.DeleteIfExists(new TableRequestOptions { RetryPolicy = new NoRetry() });
                eventTable.Create();
            }
            catch (StorageException exception)
            when (exception.InnerException is WebException)
            {
                TestContext.WriteLine("{0}", exception);
                Assert.Inconclusive("Could not connect to Azure Storage Emulator. See the output for details. Refer to the following URL for more information: http://go.microsoft.com/fwlink/?LinkId=392237");
            }

            return eventTable;
        }

        private class MessageBus : IMessageBus
        {
            private ConcurrentQueue<Envelope> _log = new ConcurrentQueue<Envelope>();

            public IEnumerable<Envelope> Log => _log;

            public Task Send(Envelope envelope, CancellationToken cancellationToken)
            {
                Task.Factory.StartNew(() => _log.Enqueue(envelope));
                return Task.CompletedTask;
            }

            public Task Send(IEnumerable<Envelope> envelopes, CancellationToken cancellationToken)
            {
                Task.Factory.StartNew(() =>
                {
                    foreach (Envelope envelope in envelopes)
                    {
                        _log.Enqueue(envelope);
                    }
                });
                return Task.CompletedTask;
            }
        }
    }
}
