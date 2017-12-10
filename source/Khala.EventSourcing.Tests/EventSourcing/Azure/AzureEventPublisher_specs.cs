namespace Khala.EventSourcing.Azure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Khala.FakeDomain;
    using Khala.FakeDomain.Events;
    using Khala.Messaging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.RetryPolicies;
    using Microsoft.WindowsAzure.Storage.Table;
    using Moq;
    using Ploeh.AutoFixture;
    using Ploeh.AutoFixture.AutoMoq;
    using Ploeh.AutoFixture.Idioms;

    [TestClass]
    public class AzureEventPublisher_specs
    {
        private static CloudStorageAccount s_storageAccount;
        private static CloudTable s_eventTable;
        private static bool s_storageEmulatorConnected;
        private IMessageSerializer _serializer;
        private IMessageBus _messageBus;
        private AzureEventPublisher _sut;

        public TestContext TestContext { get; set; }

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            try
            {
                s_storageAccount = CloudStorageAccount.DevelopmentStorageAccount;
                CloudTableClient tableClient = s_storageAccount.CreateCloudTableClient();
                s_eventTable = tableClient.GetTableReference("AzureEventStoreTestEventStore");
                s_eventTable.DeleteIfExists(new TableRequestOptions { RetryPolicy = new NoRetry() });
                s_eventTable.Create();
                s_storageEmulatorConnected = true;
            }
            catch (StorageException exception)
            when (exception.InnerException is WebException)
            {
                context.WriteLine("{0}", exception);
            }
        }

        [TestInitialize]
        public void TestInitialize()
        {
            if (s_storageEmulatorConnected == false)
            {
                Assert.Inconclusive("Could not connect to Azure Storage Emulator. See the output for details. Refer to the following URL for more information: http://go.microsoft.com/fwlink/?LinkId=392237");
            }

            _serializer = new JsonMessageSerializer();
            _messageBus = Mock.Of<IMessageBus>();
            _sut = new AzureEventPublisher(s_eventTable, _serializer, _messageBus);
        }

        [TestMethod]
        public void sut_implements_IAzureEventPublisher()
        {
            _sut.Should().BeAssignableTo<IAzureEventPublisher>();
        }

        [TestMethod]
        public void class_has_guard_clauses()
        {
            var builder = new Fixture();
            builder.Customize(new AutoMoqCustomization());
            builder.Inject(s_eventTable);
            new GuardClauseAssertion(builder).Verify(typeof(AzureEventPublisher));
        }

        private Task InsertPendingEvents(IEnumerable<Envelope> envelopes)
        {
            IEnumerable<PendingEventTableEntity> entities =
                from e in envelopes
                select PendingEventTableEntity.FromEnvelope<FakeUser>(e, _serializer);
            return InsertPendingEvents(entities);
        }

        private static Task InsertPendingEvents(IEnumerable<PendingEventTableEntity> entities)
        {
            var batchOperation = new TableBatchOperation();
            entities.ForEach(batchOperation.Insert);
            return s_eventTable.ExecuteBatchAsync(batchOperation);
        }

        private static Task DeletePendingEvents(IEnumerable<PendingEventTableEntity> entities)
        {
            var batchOperation = new TableBatchOperation();
            entities.ForEach(batchOperation.Delete);
            return s_eventTable.ExecuteBatchAsync(batchOperation);
        }

        private Task InsertPersistentEvents(IEnumerable<Envelope> envelopes)
        {
            var batchOperation = new TableBatchOperation();
            envelopes
                .Select(e => EventTableEntity.FromEnvelope<FakeUser>(e, _serializer))
                .ForEach(batchOperation.Insert);
            return s_eventTable.ExecuteBatchAsync(batchOperation);
        }

        private static IEnumerable<PendingEventTableEntity> QueryPendingEventEntities<T>(Guid sourceId)
        {
            string partitionKey = PendingEventTableEntity.GetPartitionKey(typeof(T), sourceId);
            var query = new TableQuery<PendingEventTableEntity>().Where($"PartitionKey eq '{partitionKey}'");
            return s_eventTable.ExecuteQuery(query);
        }

        private IReadOnlyCollection<DomainEvent> CreateFakeUserDomainEvents(Guid userId)
        {
            var fixture = new Fixture();
            var domainEvents = new DomainEvent[]
            {
                fixture.Create<FakeUserCreated>(),
                fixture.Create<FakeUsernameChanged>()
            };
            RaiseEvents(userId, domainEvents);
            return domainEvents;
        }

        [TestMethod]
        public async Task FlushPendingEvents_sends_pending_events()
        {
            // Arrange
            var userId = Guid.NewGuid();
            IEnumerable<DomainEvent> domainEvents = CreateFakeUserDomainEvents(userId);
            var correlationId = Guid.NewGuid();
            string contributor = Guid.NewGuid().ToString();
            var envelopes = new List<Envelope>(
                from e in domainEvents
                select new Envelope(Guid.NewGuid(), correlationId, contributor, message: e));
            await InsertPendingEvents(envelopes);
            await InsertPersistentEvents(envelopes);

            List<Envelope> messages = null;
            Mock.Get(_messageBus)
                .Setup(
                    x =>
                    x.Send(
                        It.IsAny<IEnumerable<Envelope>>(),
                        It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<Envelope>, CancellationToken>((b, t) => messages = b.ToList())
                .Returns(Task.CompletedTask);

            // Act
            await _sut.FlushPendingEvents<FakeUser>(userId, CancellationToken.None);

            // Assert
            Mock.Get(_messageBus).Verify(
                x =>
                x.Send(
                    It.IsAny<IEnumerable<Envelope>>(),
                    CancellationToken.None),
                Times.Once());
            messages.ShouldAllBeEquivalentTo(envelopes, opts => opts.RespectingRuntimeTypes());
        }

        [TestMethod]
        public async Task FlushPendingEvents_sends_only_persisted_pending_events()
        {
            // Arrange
            var userId = Guid.NewGuid();
            IEnumerable<DomainEvent> domainEvents = CreateFakeUserDomainEvents(userId);
            var envelopes = new List<Envelope>(domainEvents.Select(e => new Envelope(e)));
            await InsertPendingEvents(envelopes);
            await InsertPersistentEvents(envelopes.Take(1));

            List<Envelope> sent = null;
            Mock.Get(_messageBus)
                .Setup(
                    x =>
                    x.Send(
                        It.IsAny<IEnumerable<Envelope>>(),
                        It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<Envelope>, CancellationToken>((b, t) => sent = b.ToList())
                .Returns(Task.CompletedTask);

            // Act
            await _sut.FlushPendingEvents<FakeUser>(userId, CancellationToken.None);

            // Assert
            Mock.Get(_messageBus).Verify(
                x =>
                x.Send(
                    It.IsAny<IEnumerable<Envelope>>(),
                    CancellationToken.None),
                Times.Once());
            sent.ShouldAllBeEquivalentTo(envelopes.Take(1), opts => opts.RespectingRuntimeTypes());
        }

        [TestMethod]
        public async Task FlushPendingEvents_deletes_all_pending_events()
        {
            // Arrange
            var userId = Guid.NewGuid();
            IEnumerable<DomainEvent> domainEvents = CreateFakeUserDomainEvents(userId);
            var envelopes = new List<Envelope>(domainEvents.Select(e => new Envelope(e)));
            await InsertPendingEvents(envelopes);
            await InsertPersistentEvents(envelopes.Take(1));

            // Act
            await _sut.FlushPendingEvents<FakeUser>(userId, CancellationToken.None);

            // Assert
            IEnumerable<PendingEventTableEntity> actual = QueryPendingEventEntities<FakeUser>(userId);
            actual.Should().BeEmpty();
        }

        [TestMethod]
        public async Task FlushPendingEvents_does_not_delete_pending_events_if_fails_to_send()
        {
            // Arrange
            var userId = Guid.NewGuid();
            IEnumerable<DomainEvent> domainEvents = CreateFakeUserDomainEvents(userId);
            var envelopes = new List<Envelope>(domainEvents.Select(e => new Envelope(e)));

            var pendingEvents = new List<PendingEventTableEntity>(
                from e in envelopes
                select PendingEventTableEntity.FromEnvelope<FakeUser>(e, _serializer));
            await InsertPendingEvents(pendingEvents);

            await InsertPersistentEvents(envelopes);

            Mock.Get(_messageBus)
                .Setup(
                    x =>
                    x.Send(
                        It.IsAny<IEnumerable<Envelope>>(),
                        It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException());

            // Act
            try
            {
                await _sut.FlushPendingEvents<FakeUser>(userId, CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
            }

            // Assert
            string partitionKey = PendingEventTableEntity.GetPartitionKey(typeof(FakeUser), userId);
            var query = new TableQuery<PendingEventTableEntity>().Where($"PartitionKey eq '{partitionKey}'");
            IEnumerable<string> actual = s_eventTable.ExecuteQuery(query).Select(e => e.RowKey);
            actual.ShouldAllBeEquivalentTo(pendingEvents.Select(e => e.RowKey));
        }

        [TestMethod]
        public void FlushPendingEvents_does_not_invoke_SendBatch_if_pending_event_not_found()
        {
            // Arrange
            var userId = Guid.NewGuid();

            // Act
            Func<Task> action = () => _sut.FlushPendingEvents<FakeUser>(userId, CancellationToken.None);

            // Assert
            action.ShouldNotThrow();
            Mock.Get(_messageBus).Verify(
                x =>
                x.Send(
                    It.IsAny<IEnumerable<Envelope>>(),
                    It.IsAny<CancellationToken>()),
                Times.Never());
        }

        [TestMethod]
        public async Task FlushPendingEvents_absorbs_exception_caused_by_that_some_pending_event_already_deleted_since_loaded()
        {
            // Arrange
            var userId = Guid.NewGuid();
            IEnumerable<DomainEvent> domainEvents = CreateFakeUserDomainEvents(userId);

            var envelopes = new List<Envelope>(domainEvents.Select(e => new Envelope(e)));

            var pendingEvents = new List<PendingEventTableEntity>(
                from e in envelopes
                select PendingEventTableEntity.FromEnvelope<FakeUser>(e, _serializer));
            await InsertPendingEvents(pendingEvents);

            await InsertPersistentEvents(envelopes);

            var completionSource = new TaskCompletionSource<bool>();
            IMessageBus messageBus = new AwaitingMessageBus(completionSource.Task);
            var sut = new AzureEventPublisher(s_eventTable, _serializer, messageBus);

            // Act
            Func<Task> action = async () =>
            {
                Task flushTask = sut.FlushPendingEvents<FakeUser>(userId, CancellationToken.None);
                await DeletePendingEvents(pendingEvents.OrderBy(e => e.GetHashCode()).Take(1));
                completionSource.SetResult(true);
                await flushTask;
            };

            // Assert
            action.ShouldNotThrow();
            IEnumerable<PendingEventTableEntity> actual = QueryPendingEventEntities<FakeUser>(userId);
            actual.Should().BeEmpty();
        }

        [TestMethod]
        public async Task FlushAllPendingEvents_sends_all_pending_events()
        {
            // Arrange
            var domainEvents = new List<DomainEvent>();
            var userIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            foreach (Guid userId in userIds)
            {
                IEnumerable<DomainEvent> events = CreateFakeUserDomainEvents(userId);
                var envelopes = new List<Envelope>(events.Select(e => new Envelope(e)));
                await InsertPendingEvents(envelopes);
                await InsertPersistentEvents(envelopes);
                domainEvents.AddRange(events);
            }

            var messages = new List<IDomainEvent>();
            Mock.Get(_messageBus)
                .Setup(
                    x =>
                    x.Send(
                        It.IsAny<IEnumerable<Envelope>>(),
                        It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<Envelope>, CancellationToken>(
                    (batch, cancellationToken) =>
                    messages.AddRange(batch
                        .Select(b => b.Message)
                        .OfType<IDomainEvent>()
                        .Where(m => userIds.Contains(m.SourceId))))
                .Returns(Task.FromResult(true));

            // Act
            await _sut.FlushAllPendingEvents(CancellationToken.None);

            // Assert
            messages.ShouldAllBeEquivalentTo(domainEvents);
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

        private class AwaitingMessageBus : IMessageBus
        {
            private readonly Task _awaitable;

            public AwaitingMessageBus(Task awaitable)
            {
                _awaitable = awaitable;
            }

            public Task Send(Envelope envelope, CancellationToken cancellationToken)
            {
                return _awaitable;
            }

            public Task Send(IEnumerable<Envelope> envelopes, CancellationToken cancellationToken)
            {
                return _awaitable;
            }
        }
    }
}
