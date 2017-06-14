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
    public class AzureEventPublisher_features
    {
        private static CloudStorageAccount s_storageAccount;
        private static CloudTable s_eventTable;
        private static bool s_storageEmulatorConnected;
        private IFixture fixture;
        private IMessageSerializer serializer;
        private IMessageBus messageBus;
        private AzureEventPublisher sut;

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

            fixture = new Fixture().Customize(new AutoMoqCustomization());
            fixture.Inject(s_eventTable);
            serializer = new JsonMessageSerializer();
            messageBus = Mock.Of<IMessageBus>();
            sut = new AzureEventPublisher(s_eventTable, serializer, messageBus);
        }

        [TestMethod]
        public void sut_implments_IAzureEventPublisher()
        {
            sut.Should().BeAssignableTo<IAzureEventPublisher>();
        }

        [TestMethod]
        public void class_has_guard_clauses()
        {
            var assertion = new GuardClauseAssertion(fixture);
            assertion.Verify(typeof(AzureEventPublisher));
        }

        [TestMethod]
        public async Task PublishPendingEvents_sends_pending_events()
        {
            // Arrange
            var userId = Guid.NewGuid();

            var userCreated = fixture.Create<FakeUserCreated>();
            var usernameChanged = fixture.Create<FakeUsernameChanged>();
            var domainEvents = new DomainEvent[] { userCreated, usernameChanged };
            RaiseEvents(userId, domainEvents);

            var envelopes = new List<Envelope>(domainEvents.Select(e => new Envelope(e)));

            var batchOperation = new TableBatchOperation();
            envelopes
                .Select(e => PendingEventTableEntity.FromEnvelope<FakeUser>(e, serializer))
                .ForEach(batchOperation.Insert);
            await s_eventTable.ExecuteBatchAsync(batchOperation);

            batchOperation.Clear();
            envelopes
                .Select(e => EventTableEntity.FromEnvelope<FakeUser>(e, serializer))
                .ForEach(batchOperation.Insert);
            await s_eventTable.ExecuteBatchAsync(batchOperation);

            List<Envelope> batch = null;

            Mock.Get(messageBus)
                .Setup(
                    x =>
                    x.SendBatch(
                        It.IsAny<IEnumerable<Envelope>>(),
                        It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<Envelope>, CancellationToken>((b, t) => batch = b.ToList())
                .Returns(Task.FromResult(true));

            // Act
            await sut.PublishPendingEvents<FakeUser>(userId, CancellationToken.None);

            // Assert
            Mock.Get(messageBus).Verify(
                x =>
                x.SendBatch(
                    It.IsAny<IEnumerable<Envelope>>(),
                    CancellationToken.None),
                Times.Once());
            batch.ShouldAllBeEquivalentTo(envelopes, opts => opts.RespectingRuntimeTypes());
        }

        [TestMethod]
        public async Task PublishPendingEvents_sends_only_persisted_pending_events()
        {
            // Arrange
            var userId = Guid.NewGuid();

            var userCreated = fixture.Create<FakeUserCreated>();
            var usernameChanged = fixture.Create<FakeUsernameChanged>();
            var domainEvents = new DomainEvent[] { userCreated, usernameChanged };
            RaiseEvents(userId, domainEvents);

            var envelopes = new List<Envelope>(domainEvents.Select(e => new Envelope(e)));

            var batchOperation = new TableBatchOperation();
            envelopes
                .Select(e => PendingEventTableEntity.FromEnvelope<FakeUser>(e, serializer))
                .ForEach(batchOperation.Insert);
            await s_eventTable.ExecuteBatchAsync(batchOperation);

            batchOperation.Clear();
            envelopes
                .Take(1)
                .Select(e => EventTableEntity.FromEnvelope<FakeUser>(e, serializer))
                .ForEach(batchOperation.Insert);
            await s_eventTable.ExecuteBatchAsync(batchOperation);

            List<Envelope> batch = null;

            Mock.Get(messageBus)
                .Setup(
                    x =>
                    x.SendBatch(
                        It.IsAny<IEnumerable<Envelope>>(),
                        It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<Envelope>, CancellationToken>((b, t) => batch = b.ToList())
                .Returns(Task.FromResult(true));

            // Act
            await sut.PublishPendingEvents<FakeUser>(userId, CancellationToken.None);

            // Assert
            Mock.Get(messageBus).Verify(
                x =>
                x.SendBatch(
                    It.IsAny<IEnumerable<Envelope>>(),
                    CancellationToken.None),
                Times.Once());
            batch.ShouldAllBeEquivalentTo(envelopes.Take(1), opts => opts.RespectingRuntimeTypes());
        }

        [TestMethod]
        public async Task PublishPendingEvents_deletes_all_pending_events()
        {
            // Arrange
            var userId = Guid.NewGuid();

            var userCreated = fixture.Create<FakeUserCreated>();
            var usernameChanged = fixture.Create<FakeUsernameChanged>();
            var domainEvents = new DomainEvent[] { userCreated, usernameChanged };
            RaiseEvents(userId, domainEvents);

            var envelopes = new List<Envelope>(domainEvents.Select(e => new Envelope(e)));

            var batchOperation = new TableBatchOperation();
            envelopes
                .Select(e => PendingEventTableEntity.FromEnvelope<FakeUser>(e, serializer))
                .ForEach(batchOperation.Insert);
            await s_eventTable.ExecuteBatchAsync(batchOperation);

            batchOperation.Clear();
            envelopes
                .Take(1)
                .Select(e => EventTableEntity.FromEnvelope<FakeUser>(e, serializer))
                .ForEach(batchOperation.Insert);
            await s_eventTable.ExecuteBatchAsync(batchOperation);

            // Act
            await sut.PublishPendingEvents<FakeUser>(userId, CancellationToken.None);

            // Assert
            string partitionKey = PendingEventTableEntity.GetPartitionKey(typeof(FakeUser), userId);
            var query = new TableQuery<PendingEventTableEntity>().Where($"PartitionKey eq '{partitionKey}'");
            List<PendingEventTableEntity> actual = s_eventTable.ExecuteQuery(query).ToList();
            actual.Should().BeEmpty();
        }

        [TestMethod]
        public async Task PublishPendingEvents_does_not_delete_pending_events_if_fails_to_send()
        {
            // Arrange
            var userId = Guid.NewGuid();

            var userCreated = fixture.Create<FakeUserCreated>();
            var usernameChanged = fixture.Create<FakeUsernameChanged>();
            var domainEvents = new DomainEvent[] { userCreated, usernameChanged };
            RaiseEvents(userId, domainEvents);

            var envelopes = new List<Envelope>(domainEvents.Select(e => new Envelope(e)));

            var batchOperation = new TableBatchOperation();
            var pendingEvents = new List<PendingEventTableEntity>(
                envelopes.Select(e => PendingEventTableEntity.FromEnvelope<FakeUser>(e, serializer)));
            pendingEvents.ForEach(batchOperation.Insert);
            await s_eventTable.ExecuteBatchAsync(batchOperation);

            batchOperation.Clear();
            envelopes
                .Take(1)
                .Select(e => EventTableEntity.FromEnvelope<FakeUser>(e, serializer))
                .ForEach(batchOperation.Insert);
            await s_eventTable.ExecuteBatchAsync(batchOperation);

            Mock.Get(messageBus)
                .Setup(
                    x =>
                    x.SendBatch(
                        It.IsAny<IEnumerable<Envelope>>(),
                        It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException());

            // Act
            try
            {
                await sut.PublishPendingEvents<FakeUser>(userId, CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
            }

            // Assert
            string partitionKey = PendingEventTableEntity.GetPartitionKey(typeof(FakeUser), userId);
            var query = new TableQuery<PendingEventTableEntity>().Where($"PartitionKey eq '{partitionKey}'");
            IEnumerable<object> actual = s_eventTable.ExecuteQuery(query).Select(e => e.RowKey);
            actual.ShouldAllBeEquivalentTo(pendingEvents.Select(e => e.RowKey));
        }

        [TestMethod]
        public void PublishPendingEvents_does_not_invoke_SendBatch_if_pending_event_not_found()
        {
            // Arrange
            var userId = Guid.NewGuid();

            // Act
            Func<Task> action = () => sut.PublishPendingEvents<FakeUser>(userId, CancellationToken.None);

            // Assert
            action.ShouldNotThrow();
            Mock.Get(messageBus).Verify(
                x =>
                x.SendBatch(
                    It.IsAny<IEnumerable<Envelope>>(),
                    It.IsAny<CancellationToken>()),
                Times.Never());
        }

        [TestMethod]
        public async Task PublishPendingEvents_does_not_fails_even_if_all_events_persisted()
        {
            // Arrange
            var userId = Guid.NewGuid();

            var userCreated = fixture.Create<FakeUserCreated>();
            var usernameChanged = fixture.Create<FakeUsernameChanged>();
            var domainEvents = new DomainEvent[] { userCreated, usernameChanged };
            RaiseEvents(userId, domainEvents);

            var envelopes = new List<Envelope>(domainEvents.Select(e => new Envelope(e)));

            var batchOperation = new TableBatchOperation();
            envelopes
                .Select(e => PendingEventTableEntity.FromEnvelope<FakeUser>(e, serializer))
                .ForEach(batchOperation.Insert);
            await s_eventTable.ExecuteBatchAsync(batchOperation);

            batchOperation.Clear();
            envelopes
                .Select(e => EventTableEntity.FromEnvelope<FakeUser>(e, serializer))
                .ForEach(batchOperation.Insert);
            await s_eventTable.ExecuteBatchAsync(batchOperation);

            // Act
            Func<Task> action = () => sut.PublishPendingEvents<FakeUser>(userId, CancellationToken.None);

            // Assert
            action.ShouldNotThrow();
        }

        [TestMethod]
        public async Task PublishAllEvents_sends_pending_events()
        {
            // Arrange
            var domainEvents = new List<DomainEvent>();

            List<Guid> users = fixture.CreateMany<Guid>().ToList();

            foreach (Guid userId in users)
            {
                var userCreated = fixture.Create<FakeUserCreated>();
                var usernameChanged = fixture.Create<FakeUsernameChanged>();
                var events = new DomainEvent[] { userCreated, usernameChanged };
                RaiseEvents(userId, events);

                var envelopes = new List<Envelope>(events.Select(e => new Envelope(e)));

                var batchOperation = new TableBatchOperation();
                envelopes
                    .Select(e => PendingEventTableEntity.FromEnvelope<FakeUser>(e, serializer))
                    .ForEach(batchOperation.Insert);
                await s_eventTable.ExecuteBatchAsync(batchOperation);

                batchOperation.Clear();
                envelopes
                    .Select(e => EventTableEntity.FromEnvelope<FakeUser>(e, serializer))
                    .ForEach(batchOperation.Insert);
                await s_eventTable.ExecuteBatchAsync(batchOperation);

                domainEvents.AddRange(events);
            }

            var messages = new List<IDomainEvent>();

            Mock.Get(messageBus)
                .Setup(
                    x =>
                    x.SendBatch(
                        It.IsAny<IEnumerable<Envelope>>(),
                        It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<Envelope>, CancellationToken>(
                    (batch, cancellationToken) =>
                    messages.AddRange(batch
                        .Select(b => b.Message)
                        .OfType<IDomainEvent>()
                        .Where(m => users.Contains(m.SourceId))))
                .Returns(Task.FromResult(true));

            // Act
            await sut.PublishAllEvents(CancellationToken.None);

            // Assert
            messages.Should().OnlyContain(e => e is IDomainEvent);
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
    }
}
