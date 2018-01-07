namespace Khala.EventSourcing.Azure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AutoFixture;
    using AutoFixture.AutoMoq;
    using FluentAssertions;
    using Khala.FakeDomain;
    using Khala.FakeDomain.Events;
    using Khala.Messaging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.RetryPolicies;
    using Microsoft.WindowsAzure.Storage.Table;

    [TestClass]
    public class AzureEventStore_specs
    {
        private static CloudStorageAccount s_storageAccount;
        private static CloudTable s_eventTable;
        private IMessageSerializer _serializer;
        private AzureEventStore _sut;

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            try
            {
                s_storageAccount = CloudStorageAccount.DevelopmentStorageAccount;
                CloudTableClient tableClient = s_storageAccount.CreateCloudTableClient();
                s_eventTable = tableClient.GetTableReference("AzureEventStoreTestEventStore");
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

        [TestInitialize]
        public void TestInitialize()
        {
            _serializer = new JsonMessageSerializer();
            _sut = new AzureEventStore(s_eventTable, _serializer);
        }

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void sut_implements_IAzureEventStore()
        {
            typeof(AzureEventStore).Should().Implement<IAzureEventStore>();
        }

        [TestMethod]
        public void SaveEvents_fails_if_events_contains_null()
        {
            IFixture fixture = new Fixture().Customize(new AutoMoqCustomization());
            IEnumerable<IDomainEvent> events = Enumerable
                .Repeat(fixture.Create<IDomainEvent>(), 10)
                .Concat(new[] { default(IDomainEvent) })
                .OrderBy(_ => fixture.Create<int>());

            Func<Task> action = () => _sut.SaveEvents<FakeUser>(events);

            action.ShouldThrow<ArgumentException>().Where(x => x.ParamName == "events");
        }

        [TestMethod]
        public async Task SaveEvents_inserts_persistent_event_entities_correctly()
        {
            // Arrange
            var fixture = new Fixture();
            var user = new FakeUser(Guid.NewGuid(), fixture.Create<string>());
            user.ChangeUsername(fixture.Create<string>());
            IList<IDomainEvent> domainEvents = user.FlushPendingEvents().ToList();
            var operationId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            string contributor = fixture.Create<string>();

            // Act
            await _sut.SaveEvents<FakeUser>(domainEvents, operationId, correlationId, contributor);

            // Assert
            string filter = PersistentEvent.GetFilter(typeof(FakeUser), user.Id);
            var query = new TableQuery<PersistentEvent> { FilterString = filter };
            IEnumerable<PersistentEvent> actual = await s_eventTable.ExecuteQuerySegmentedAsync(query, default);
            actual.ShouldAllBeEquivalentTo(
                from domainEvent in domainEvents
                let envelope = new Envelope<IDomainEvent>(
                    Guid.NewGuid(),
                    domainEvent,
                    operationId,
                    correlationId,
                    contributor)
                select PersistentEvent.Create(typeof(FakeUser), envelope, _serializer),
                opts => opts
                    .Excluding(e => e.MessageId)
                    .Excluding(e => e.Timestamp)
                    .Excluding(e => e.ETag)
                    .WithStrictOrdering());
        }

        [TestMethod]
        public async Task SaveEvents_inserts_pending_event_entities_correctly()
        {
            // Arrange
            var fixture = new Fixture();
            var user = new FakeUser(Guid.NewGuid(), fixture.Create<string>());
            user.ChangeUsername(fixture.Create<string>());
            IList<IDomainEvent> domainEvents = user.FlushPendingEvents().ToList();
            var operationId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            string contributor = fixture.Create<string>();

            // Act
            await _sut.SaveEvents<FakeUser>(domainEvents, operationId, correlationId, contributor);

            // Assert
            string filter = PendingEvent.GetFilter(typeof(FakeUser), user.Id);
            var query = new TableQuery<PendingEvent> { FilterString = filter };
            IEnumerable<PendingEvent> actual = await s_eventTable.ExecuteQuerySegmentedAsync(query, default);
            actual.ShouldAllBeEquivalentTo(
                from domainEvent in domainEvents
                let envelope = new Envelope<IDomainEvent>(
                    Guid.NewGuid(),
                    domainEvent,
                    operationId,
                    correlationId,
                    contributor)
                select PendingEvent.Create(typeof(FakeUser), envelope, _serializer),
                opts => opts
                    .Excluding(e => e.MessageId)
                    .Excluding(e => e.Timestamp)
                    .Excluding(e => e.ETag)
                    .WithStrictOrdering());
        }

        [TestMethod]
        public async Task SaveEvents_does_not_insert_persistent_event_entities_if_fails_to_insert_pending_event_entities()
        {
            // Arrange
            var fixture = new Fixture();
            var user = new FakeUser(Guid.NewGuid(), fixture.Create<string>());
            user.ChangeUsername(fixture.Create<string>());
            IList<IDomainEvent> domainEvents = user.FlushPendingEvents().ToList();

            IDomainEvent conflicting = domainEvents[new Random().Next(domainEvents.Count)];
            var batch = new TableBatchOperation();
            batch.Insert(new TableEntity
            {
                PartitionKey = AggregateEntity.GetPartitionKey(typeof(FakeUser), user.Id),
                RowKey = PendingEvent.GetRowKey(conflicting.Version),
            });
            await s_eventTable.ExecuteBatchAsync(batch);

            // Act
            Func<Task> action = () => _sut.SaveEvents<FakeUser>(domainEvents);

            // Assert
            action.ShouldThrow<StorageException>();
            string filter = PersistentEvent.GetFilter(typeof(FakeUser), user.Id);
            var query = new TableQuery<PersistentEvent> { FilterString = filter };
            IEnumerable<PersistentEvent> actual = await s_eventTable.ExecuteQuerySegmentedAsync(query, default);
            actual.Should().BeEmpty();
        }

        [TestMethod]
        public async Task SaveEvents_does_not_insert_pending_event_entities_if_fails_to_insert_persistent_event_entities()
        {
            // Arrange
            var fixture = new Fixture();
            var user = new FakeUser(Guid.NewGuid(), fixture.Create<string>());
            user.ChangeUsername(fixture.Create<string>());
            IList<IDomainEvent> domainEvents = user.FlushPendingEvents().ToList();

            IDomainEvent conflicting = domainEvents[new Random().Next(domainEvents.Count)];
            var batch = new TableBatchOperation();
            batch.Insert(new TableEntity
            {
                PartitionKey = AggregateEntity.GetPartitionKey(typeof(FakeUser), user.Id),
                RowKey = PersistentEvent.GetRowKey(conflicting.Version),
            });
            await s_eventTable.ExecuteBatchAsync(batch);

            // Act
            Func<Task> action = () => _sut.SaveEvents<FakeUser>(domainEvents);

            // Assert
            action.ShouldThrow<StorageException>();
            string filter = PendingEvent.GetFilter(typeof(FakeUser), user.Id);
            var query = new TableQuery<PendingEvent> { FilterString = filter };
            IEnumerable<PendingEvent> actual = await s_eventTable.ExecuteQuerySegmentedAsync(query, default);
            actual.Should().BeEmpty();
        }

        [TestMethod]
        public void SaveEvents_does_not_fail_even_if_events_empty()
        {
            DomainEvent[] events = new DomainEvent[] { };
            Func<Task> action = () => _sut.SaveEvents<FakeUser>(events);
            action.ShouldNotThrow();
        }

        [TestMethod]
        public async Task SaveEvents_inserts_correlation_entity_correctly()
        {
            // Arrange
            var fixture = new Fixture();
            var user = new FakeUser(Guid.NewGuid(), fixture.Create<string>());
            IList<IDomainEvent> domainEvents = user.FlushPendingEvents().ToList();
            var operationId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            string contributor = fixture.Create<string>();

            // Act
            await _sut.SaveEvents<FakeUser>(domainEvents, operationId, correlationId, contributor);

            // Assert
            string filter = Correlation.GetFilter(typeof(FakeUser), user.Id, correlationId);
            var query = new TableQuery<Correlation> { FilterString = filter };
            IEnumerable<Correlation> actual = await s_eventTable.ExecuteQuerySegmentedAsync(query, default);
            actual.Should().ContainSingle().Which.ShouldBeEquivalentTo(
                new { CorrelationId = correlationId },
                opts => opts.ExcludingMissingMembers());
        }

        [TestMethod]
        public async Task SaveEvents_throws_DuplicateCorrelationException_if_correlation_duplicate()
        {
            // Arrange
            var fixture = new Fixture();
            var user = new FakeUser(Guid.NewGuid(), fixture.Create<string>());
            user.ChangeUsername(fixture.Create<string>());
            IList<IDomainEvent> domainEvents = user.FlushPendingEvents().ToList();
            var correlationId = Guid.NewGuid();
            await _sut.SaveEvents<FakeUser>(domainEvents.Take(1), correlationId: correlationId);

            // Act
            Func<Task> action = () => _sut.SaveEvents<FakeUser>(domainEvents.Skip(1), correlationId: correlationId);

            // Assert
            action.ShouldThrow<DuplicateCorrelationException>().Where(
                x =>
                x.SourceType == typeof(FakeUser) &&
                x.SourceId == user.Id &&
                x.CorrelationId == correlationId &&
                x.InnerException is StorageException);
        }

        [TestMethod]
        public async Task SaveEvents_does_not_insert_persistent_event_entities_if_fails_to_insert_correlation_entity()
        {
            // Arrange
            var fixture = new Fixture();
            var user = new FakeUser(Guid.NewGuid(), fixture.Create<string>());
            user.ChangeUsername(fixture.Create<string>());
            IList<IDomainEvent> domainEvents = user.FlushPendingEvents().ToList();
            var correlationId = Guid.NewGuid();

            var batch = new TableBatchOperation();
            batch.Insert(new TableEntity
            {
                PartitionKey = AggregateEntity.GetPartitionKey(typeof(FakeUser), user.Id),
                RowKey = Correlation.GetRowKey(correlationId),
            });
            await s_eventTable.ExecuteBatchAsync(batch);

            // Act
            Func<Task> action = () => _sut.SaveEvents<FakeUser>(domainEvents, correlationId: correlationId);

            // Assert
            action.ShouldThrow<DuplicateCorrelationException>();
            string filter = PersistentEvent.GetFilter(typeof(FakeUser), user.Id);
            var query = new TableQuery<PersistentEvent> { FilterString = filter };
            IEnumerable<PersistentEvent> actual = await s_eventTable.ExecuteQuerySegmentedAsync(query, default);
            actual.Should().BeEmpty();
        }

        [TestMethod]
        public async Task SaveEvents_does_not_insert_pending_event_entities_if_fails_to_insert_correlation_entities()
        {
            // Arrange
            var fixture = new Fixture();
            var user = new FakeUser(Guid.NewGuid(), fixture.Create<string>());
            user.ChangeUsername(fixture.Create<string>());
            IList<IDomainEvent> domainEvents = user.FlushPendingEvents().ToList();
            var correlationId = Guid.NewGuid();

            var batch = new TableBatchOperation();
            batch.Insert(new TableEntity
            {
                PartitionKey = AggregateEntity.GetPartitionKey(typeof(FakeUser), user.Id),
                RowKey = Correlation.GetRowKey(correlationId),
            });
            await s_eventTable.ExecuteBatchAsync(batch);

            // Act
            Func<Task> action = () => _sut.SaveEvents<FakeUser>(domainEvents, correlationId: correlationId);

            // Assert
            action.ShouldThrow<DuplicateCorrelationException>();
            string filter = PendingEvent.GetFilter(typeof(FakeUser), user.Id);
            var query = new TableQuery<PendingEvent> { FilterString = filter };
            IEnumerable<PendingEvent> actual = await s_eventTable.ExecuteQuerySegmentedAsync(query, default);
            actual.Should().BeEmpty();
        }

        [TestMethod]
        public void SaveEvents_fails_if_versions_not_sequential()
        {
            // Arrange
            DomainEvent[] events = new DomainEvent[]
            {
                new FakeUserCreated { Version = 1 },
                new FakeUsernameChanged { Version = 2 },
                new FakeUsernameChanged { Version = 4 },
            };

            // Act
            Func<Task> action = () => _sut.SaveEvents<FakeUser>(events);

            // Assert
            action.ShouldThrow<ArgumentException>().Where(x => x.ParamName == "events");
        }

        [TestMethod]
        public void SaveEvents_fails_if_events_not_have_same_source_id()
        {
            // Arrange
            DomainEvent[] events = new DomainEvent[]
            {
                new FakeUserCreated { Version = 1 },
                new FakeUsernameChanged { Version = 2 },
            };

            // Act
            Func<Task> action = () => _sut.SaveEvents<FakeUser>(events);

            // Assert
            action.ShouldThrow<ArgumentException>().Where(x => x.ParamName == "events");
        }

        [TestMethod]
        public async Task LoadEvents_restores_domain_events_correctly()
        {
            // Arrange
            var fixture = new Fixture();
            var user = new FakeUser(Guid.NewGuid(), fixture.Create<string>());
            user.ChangeUsername(fixture.Create<string>());
            IList<IDomainEvent> domainEvents = user.FlushPendingEvents().ToList();
            await _sut.SaveEvents<FakeUser>(domainEvents);

            // Act
            IEnumerable<IDomainEvent> actual = await _sut.LoadEvents<FakeUser>(user.Id);

            // Assert
            actual.Should().BeInAscendingOrder(e => e.Version);
            actual.ShouldAllBeEquivalentTo(domainEvents);
        }

        [TestMethod]
        public async Task LoadEvents_correctly_restores_domain_events_after_specified_version()
        {
            // Arrange
            var fixture = new Fixture();
            var user = new FakeUser(Guid.NewGuid(), fixture.Create<string>());
            user.ChangeUsername(fixture.Create<string>());
            IList<IDomainEvent> domainEvents = user.FlushPendingEvents().ToList();
            await _sut.SaveEvents<FakeUser>(domainEvents);

            // Act
            IEnumerable<IDomainEvent> actual = await _sut.LoadEvents<FakeUser>(user.Id, 1);

            // Assert
            actual.Should().BeInAscendingOrder(e => e.Version);
            actual.ShouldAllBeEquivalentTo(domainEvents.Skip(1));
        }
    }
}
