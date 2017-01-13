using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.WindowsAzure.Storage.Table;
using Moq;
using Ploeh.AutoFixture;
using Ploeh.AutoFixture.AutoMoq;
using Ploeh.AutoFixture.Idioms;
using ReactiveArchitecture.EventSourcing.Messaging;
using ReactiveArchitecture.FakeDomain;
using ReactiveArchitecture.FakeDomain.Events;

namespace ReactiveArchitecture.EventSourcing.Azure
{
    [TestClass]
    public class AzureEventPublisher_features
    {
        private static CloudStorageAccount s_storageAccount;
        private static CloudTable s_eventTable;
        private static bool s_storageEmulatorConnected;
        private IFixture fixture;
        private JsonMessageSerializer serializer;
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
        public async Task PublishPendingEvents_sends_pending_events_correctly()
        {
            // Arrange
            var userId = Guid.NewGuid();

            var userCreated = fixture.Create<FakeUserCreated>();
            var usernameChanged = fixture.Create<FakeUsernameChanged>();
            var domainEvents = new DomainEvent[] { userCreated, usernameChanged };
            RaiseEvents(userId, domainEvents);

            var serializer = new JsonMessageSerializer();

            var batchOperation = new TableBatchOperation();
            domainEvents
                .Select(e => PendingEventTableEntity.FromDomainEvent<FakeUser>(e, serializer))
                .ForEach(batchOperation.Insert);
            await s_eventTable.ExecuteBatchAsync(batchOperation);

            List<object> batch = null;

            Mock.Get(messageBus)
                .Setup(x => x.SendBatch(It.IsAny<IEnumerable<object>>()))
                .Callback<IEnumerable<object>>(b => batch = b.ToList())
                .Returns(Task.FromResult(true));

            // Act
            await sut.PublishPendingEvents<FakeUser>(userId);

            // Assert
            Mock.Get(messageBus).Verify(
                x =>
                x.SendBatch(It.IsAny<IEnumerable<object>>()),
                Times.Once());
            batch.Should().OnlyContain(e => e is IDomainEvent);
            batch.Cast<IDomainEvent>().Should().BeInAscendingOrder(e => e.Version);
            batch.ShouldAllBeEquivalentTo(domainEvents);
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

            var serializer = new JsonMessageSerializer();

            var batchOperation = new TableBatchOperation();
            domainEvents
                .Select(e => PendingEventTableEntity.FromDomainEvent<FakeUser>(e, serializer))
                .ForEach(batchOperation.Insert);
            await s_eventTable.ExecuteBatchAsync(batchOperation);

            // Act
            await sut.PublishPendingEvents<FakeUser>(userId);

            // Assert
            string partitionKey = PendingEventTableEntity.GetPartitionKey(typeof(FakeUser), userId);
            var query = new TableQuery<PendingEventTableEntity>().Where($"PartitionKey eq '{partitionKey}'");
            List<PendingEventTableEntity> actual = s_eventTable.ExecuteQuery(query).ToList();
            actual.Should().BeEmpty();
        }

        [TestMethod]
        public async Task PublishPendingEvents_does_not_deletes_pending_events_if_fails_to_send()
        {
            // Arrange
            var userId = Guid.NewGuid();

            var userCreated = fixture.Create<FakeUserCreated>();
            var usernameChanged = fixture.Create<FakeUsernameChanged>();
            var domainEvents = new DomainEvent[] { userCreated, usernameChanged };
            RaiseEvents(userId, domainEvents);

            var serializer = new JsonMessageSerializer();

            var batchOperation = new TableBatchOperation();
            domainEvents
                .Select(e => PendingEventTableEntity.FromDomainEvent<FakeUser>(e, serializer))
                .ForEach(batchOperation.Insert);
            await s_eventTable.ExecuteBatchAsync(batchOperation);

            Mock.Get(messageBus)
                .Setup(x => x.SendBatch(It.IsAny<IEnumerable<object>>()))
                .Throws(new InvalidOperationException());

            // Act
            try
            {
                await sut.PublishPendingEvents<FakeUser>(userId);
            }
            catch (InvalidOperationException)
            {
            }

            string partitionKey = PendingEventTableEntity.GetPartitionKey(typeof(FakeUser), userId);
            var query = new TableQuery<PendingEventTableEntity>().Where($"PartitionKey eq '{partitionKey}'");
            IEnumerable<object> actual = s_eventTable
                .ExecuteQuery(query)
                .Select(e => new
                {
                    e.RowKey,
                    e.EventType,
                    e.RaisedAt,
                    Payload = serializer.Deserialize(e.PayloadJson)
                });

            IEnumerable<object> expected = domainEvents
                .Select(e => new
                {
                    RowKey = EventTableEntity.GetRowKey(e.Version),
                    EventType = e.GetType().FullName,
                    e.RaisedAt,
                    Payload = e
                });

            actual.ShouldAllBeEquivalentTo(expected);
        }

        [TestMethod]
        public void PublishPendingEvents_does_not_invokes_SendBatch_if_pending_event_not_found()
        {
            // Arrange
            var userId = Guid.NewGuid();

            // Act
            Func<Task> action = () => sut.PublishPendingEvents<FakeUser>(userId);

            // Assert
            action.ShouldNotThrow();
            Mock.Get(messageBus).Verify(
                x => x.SendBatch(It.IsAny<IEnumerable<object>>()),
                Times.Never());
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
