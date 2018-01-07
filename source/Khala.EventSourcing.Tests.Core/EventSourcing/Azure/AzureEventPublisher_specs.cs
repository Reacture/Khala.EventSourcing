namespace Khala.EventSourcing.Azure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using AutoFixture.AutoMoq;
    using AutoFixture.Idioms;
    using FluentAssertions;
    using Khala.FakeDomain;
    using Khala.Messaging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.RetryPolicies;
    using Microsoft.WindowsAzure.Storage.Table;
    using Moq;

    [TestClass]
    public class AzureEventPublisher_specs
    {
        private static IMessageSerializer s_serializer;
        private static CloudStorageAccount s_storageAccount;
        private static CloudTable s_eventTable;

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            s_serializer = new JsonMessageSerializer();

            try
            {
                s_storageAccount = CloudStorageAccount.DevelopmentStorageAccount;
                CloudTableClient tableClient = s_storageAccount.CreateCloudTableClient();
                s_eventTable = tableClient.GetTableReference("AzureEventPublisherTestEventStore");
                await s_eventTable.DeleteIfExistsAsync(
                    new TableRequestOptions { RetryPolicy = new NoRetry() },
                    operationContext: default);
                await s_eventTable.CreateAsync();
            }
            catch (StorageException exception)
            {
                context.WriteLine($"{exception}");
                Assert.Inconclusive("Could not connect to Azure Storage Emulator. See the output for details. Refer to the following URL for more information: http://go.microsoft.com/fwlink/?LinkId=392237");
            }
        }

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void sut_implements_IAzureEventPublisher()
        {
            typeof(AzureEventPublisher).Should().Implement<IAzureEventPublisher>();
        }

        [TestMethod]
        public void class_has_guard_clauses()
        {
            IFixture builder = new Fixture().Customize(new AutoMoqCustomization());
            builder.Inject(s_eventTable);
            new GuardClauseAssertion(builder).Verify(typeof(AzureEventPublisher));
        }

        [TestMethod]
        public async Task FlushPendingEvents_sends_all_pending_events_correctly()
        {
            // Arrange
            var messageBus = new MessageLogger();
            var sut = new AzureEventPublisher(s_eventTable, s_serializer, messageBus);

            var fixture = new Fixture();
            var user = new FakeUser(Guid.NewGuid(), fixture.Create<string>());
            user.ChangeUsername(fixture.Create<string>());

            var operationId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            string contributor = fixture.Create<string>();

            var envelopes = new List<Envelope<IDomainEvent>>(
                from domainEvent in user.FlushPendingEvents()
                let messageId = Guid.NewGuid()
                select new Envelope<IDomainEvent>(messageId, domainEvent, operationId, correlationId, contributor));

            var batch = new TableBatchOperation();
            foreach (Envelope<IDomainEvent> envelope in envelopes)
            {
                batch.Insert(PendingEvent.Create(typeof(FakeUser), envelope, s_serializer));
            }

            await s_eventTable.ExecuteBatchAsync(batch);

            // Act
            await sut.FlushPendingEvents<FakeUser>(user.Id);

            // Assert
            messageBus.Log.ShouldAllBeEquivalentTo(envelopes);
        }

        [TestMethod]
        public async Task FlushPendingEvents_deletes_all_pending_events()
        {
            // Arrange
            var sut = new AzureEventPublisher(s_eventTable, s_serializer, Mock.Of<IMessageBus>());

            var fixture = new Fixture();
            var user = new FakeUser(Guid.NewGuid(), fixture.Create<string>());
            user.ChangeUsername(fixture.Create<string>());

            var envelopes = new List<Envelope<IDomainEvent>>(
                from domainEvent in user.FlushPendingEvents()
                let messageId = Guid.NewGuid()
                select new Envelope<IDomainEvent>(messageId, domainEvent, default, default, default));

            var batch = new TableBatchOperation();
            foreach (Envelope<IDomainEvent> envelope in envelopes)
            {
                batch.Insert(PendingEvent.Create(typeof(FakeUser), envelope, s_serializer));
            }

            await s_eventTable.ExecuteBatchAsync(batch);

            // Act
            await sut.FlushPendingEvents<FakeUser>(user.Id);

            // Assert
            string filter = PendingEvent.GetFilter(typeof(FakeUser), user.Id);
            var query = new TableQuery { FilterString = filter };
            TableQuerySegment actual = await s_eventTable.ExecuteQuerySegmentedAsync(query, default);
            actual.Results.Should().BeEmpty();
        }

        [TestMethod]
        public async Task FlushPendingEvents_does_not_delete_pending_events_if_fails_to_send()
        {
            // Arrange
            var exception = new InvalidOperationException();
            IMessageBus messageBus = Mock.Of<IMessageBus>(
                x =>
                x.Send(It.IsAny<IEnumerable<Envelope>>(), default) == Task.FromException(exception));
            var sut = new AzureEventPublisher(s_eventTable, s_serializer, messageBus);

            var fixture = new Fixture();
            var user = new FakeUser(Guid.NewGuid(), fixture.Create<string>());
            user.ChangeUsername(fixture.Create<string>());

            var operationId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            string contributor = fixture.Create<string>();

            var pendingEvents = new List<PendingEvent>(
                from message in user.FlushPendingEvents()
                let messageId = Guid.NewGuid()
                let envelope = new Envelope<IDomainEvent>(messageId, message, operationId, correlationId, contributor)
                select PendingEvent.Create(typeof(FakeUser), envelope, s_serializer));

            var batch = new TableBatchOperation();
            foreach (PendingEvent pendingEvent in pendingEvents)
            {
                batch.Insert(pendingEvent);
            }

            await s_eventTable.ExecuteBatchAsync(batch);

            // Act
            Func<Task> action = () => sut.FlushPendingEvents<FakeUser>(user.Id);

            // Assert
            action.ShouldThrow<InvalidOperationException>();
            string filter = PendingEvent.GetFilter(typeof(FakeUser), user.Id);
            var query = new TableQuery<PendingEvent> { FilterString = filter };
            TableQuerySegment<PendingEvent> actual = await s_eventTable.ExecuteQuerySegmentedAsync(query, default);
            actual.ShouldAllBeEquivalentTo(pendingEvents);
        }

        [TestMethod]
        public void FlushPendingEvents_does_not_invoke_Send_if_pending_event_not_found()
        {
            // Arrange
            IMessageBus messageBus = Mock.Of<IMessageBus>();
            var sut = new AzureEventPublisher(s_eventTable, s_serializer, messageBus);

            // Act
            Func<Task> action = () => sut.FlushPendingEvents<FakeUser>(Guid.NewGuid());

            // Assert
            action.ShouldNotThrow();
            Mock.Get(messageBus).Verify(
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
            var messageBus = new CompletableMessageBus();
            var sut = new AzureEventPublisher(s_eventTable, s_serializer, messageBus);

            var fixture = new Fixture();
            var user = new FakeUser(Guid.NewGuid(), fixture.Create<string>());
            user.ChangeUsername(fixture.Create<string>());

            var pendingEvents = new List<PendingEvent>(
                from message in user.FlushPendingEvents()
                let messageId = Guid.NewGuid()
                let envelope = new Envelope<IDomainEvent>(messageId, message, default, default, default)
                select PendingEvent.Create(typeof(FakeUser), envelope, s_serializer));

            var batch = new TableBatchOperation();
            foreach (PendingEvent pendingEvent in pendingEvents)
            {
                batch.Insert(pendingEvent);
            }

            await s_eventTable.ExecuteBatchAsync(batch);

            // Act
            Func<Task> action = async () =>
            {
                Task flushTask = sut.FlushPendingEvents<FakeUser>(user.Id, CancellationToken.None);
                await s_eventTable.ExecuteAsync(TableOperation.Delete(pendingEvents.OrderBy(e => e.GetHashCode()).First()));
                messageBus.Complete();
                await flushTask;
            };

            // Assert
            action.ShouldNotThrow();
            string filter = PendingEvent.GetFilter(typeof(FakeUser), user.Id);
            var query = new TableQuery { FilterString = filter };
            TableQuerySegment actual = await s_eventTable.ExecuteQuerySegmentedAsync(query, default);
            actual.Should().BeEmpty();
        }

        [TestMethod]
        public async Task FlushAllPendingEvents_sends_all_pending_events()
        {
            // Arrange
            await s_eventTable.DeleteIfExistsAsync();
            await s_eventTable.CreateAsync();

            var messageBus = new MessageLogger();
            var sut = new AzureEventPublisher(s_eventTable, s_serializer, messageBus);

            var expected = new List<Envelope<IDomainEvent>>();
            var fixture = new Fixture();
            var userIds = fixture.CreateMany<Guid>().ToList();
            foreach (Guid userId in userIds)
            {
                var user = new FakeUser(userId, fixture.Create<string>());
                user.ChangeUsername(fixture.Create<string>());

                var operationId = Guid.NewGuid();
                var correlationId = Guid.NewGuid();
                string contributor = fixture.Create<string>();

                var envelopes = new List<Envelope<IDomainEvent>>(
                    from message in user.FlushPendingEvents()
                    let messageId = Guid.NewGuid()
                    select new Envelope<IDomainEvent>(messageId, message, operationId, correlationId, contributor));

                expected.AddRange(envelopes);

                var batch = new TableBatchOperation();
                foreach (Envelope<IDomainEvent> envelope in envelopes)
                {
                    batch.Insert(PendingEvent.Create(typeof(FakeUser), envelope, s_serializer));
                }

                await s_eventTable.ExecuteBatchAsync(batch);
            }

            // Act
            await sut.FlushAllPendingEvents(CancellationToken.None);

            // Assert
            messageBus.Log.ShouldAllBeEquivalentTo(expected);
        }
    }
}
