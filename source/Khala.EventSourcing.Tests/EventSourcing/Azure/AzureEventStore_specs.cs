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
    using Ploeh.AutoFixture;
    using Ploeh.AutoFixture.AutoMoq;
    using Ploeh.AutoFixture.Idioms;

    [TestClass]
    public class AzureEventStore_specs
    {
        private static CloudStorageAccount s_storageAccount;
        private static CloudTable s_eventTable;
        private static bool s_storageEmulatorConnected;
        private IFixture _fixture;
        private IMessageSerializer _serializer;
        private AzureEventStore _sut;
        private Guid _userId;

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

            _fixture = new Fixture().Customize(new AutoMoqCustomization());
            _fixture.Inject(s_eventTable);
            _serializer = new JsonMessageSerializer();
            _sut = new AzureEventStore(s_eventTable, _serializer);
            _userId = Guid.NewGuid();
        }

        [TestMethod]
        public void sut_implements_IAzureEventStore()
        {
            _sut.Should().BeAssignableTo<IAzureEventStore>();
        }

        [TestMethod]
        public void class_has_guard_clauses()
        {
            var assertion = new GuardClauseAssertion(_fixture);
            assertion.Verify(typeof(AzureEventStore));
        }

        [TestMethod]
        public void SaveEvents_fails_if_events_contains_null()
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization());
            var random = new Random();
            IEnumerable<IDomainEvent> events = Enumerable
                .Repeat(fixture.Create<IDomainEvent>(), 10)
                .Concat(new[] { default(IDomainEvent) })
                .OrderBy(_ => random.Next());

            Func<Task> action = () => _sut.SaveEvents<FakeUser>(events, null, CancellationToken.None);

            action.ShouldThrow<ArgumentException>().Where(x => x.ParamName == "events");
        }

        [TestMethod]
        public async Task SaveEvents_inserts_pending_event_entities_correctly()
        {
            // Arrange
            var created = _fixture.Create<FakeUserCreated>();
            var usernameChanged = _fixture.Create<FakeUsernameChanged>();
            var events = new DomainEvent[] { created, usernameChanged };
            var correlationId = Guid.NewGuid();
            RaiseEvents(_userId, events);

            // Act
            await _sut.SaveEvents<FakeUser>(events, correlationId);

            // Assert
            string partitionKey = PendingEventTableEntity.GetPartitionKey(typeof(FakeUser), _userId);
            var query = new TableQuery<PendingEventTableEntity>().Where($"PartitionKey eq '{partitionKey}'");
            IEnumerable<PendingEventTableEntity> pendingEvents = s_eventTable.ExecuteQuery(query);
            foreach (var t in pendingEvents.Zip(events, (pending, source) =>
                              new { Pending = pending, Source = source }))
            {
                var actual = new
                {
                    t.Pending.RowKey,
                    t.Pending.PersistentPartition,
                    t.Pending.Version,
                    t.Pending.CorrelationId,
                    Message = _serializer.Deserialize(t.Pending.EventJson)
                };
                actual.ShouldBeEquivalentTo(new
                {
                    RowKey = PendingEventTableEntity.GetRowKey(t.Source.Version),
                    PersistentPartition = EventTableEntity.GetPartitionKey(typeof(FakeUser), _userId),
                    t.Source.Version,
                    CorrelationId = correlationId,
                    Message = t.Source
                },
                opts => opts.RespectingRuntimeTypes());
            }
        }

        [TestMethod]
        public async Task SaveEvents_inserts_event_entities_correctly()
        {
            // Arrange
            var created = _fixture.Create<FakeUserCreated>();
            var usernameChanged = _fixture.Create<FakeUsernameChanged>();
            var events = new DomainEvent[] { created, usernameChanged };
            var correlationId = Guid.NewGuid();
            RaiseEvents(_userId, events);

            // Act
            await _sut.SaveEvents<FakeUser>(events, correlationId);

            // Assert
            string partitionKey = EventTableEntity.GetPartitionKey(typeof(FakeUser), _userId);
            var query = new TableQuery<EventTableEntity>().Where($"PartitionKey eq '{partitionKey}'");
            IEnumerable<EventTableEntity> persistentEvents = s_eventTable.ExecuteQuery(query);
            foreach (var t in persistentEvents.Zip(events, (persistent, source)
                           => new { Persistent = persistent, Source = source }))
            {
                var actual = new
                {
                    t.Persistent.RowKey,
                    t.Persistent.Version,
                    t.Persistent.EventType,
                    t.Persistent.CorrelationId,
                    Event = (DomainEvent)_serializer.Deserialize(t.Persistent.EventJson),
                    t.Persistent.RaisedAt
                };
                actual.ShouldBeEquivalentTo(new
                {
                    RowKey = EventTableEntity.GetRowKey(t.Source.Version),
                    t.Source.Version,
                    EventType = t.Source.GetType().FullName,
                    CorrelationId = correlationId,
                    Event = t.Source,
                    t.Source.RaisedAt
                });
            }
        }

        [TestMethod]
        public void SaveEvents_does_not_insert_event_entities_if_fails_to_insert_pending_events()
        {
            Assert.Inconclusive();
        }

        [TestMethod]
        public void SaveEvents_does_not_fail_even_if_events_empty()
        {
            var events = new DomainEvent[] { };
            Func<Task> action = () => _sut.SaveEvents<FakeUser>(events);
            action.ShouldNotThrow();
        }

        [TestMethod]
        public async Task SaveEvents_inserts_CorrelationTableEntity_correctly()
        {
            // Arrange
            var created = _fixture.Create<FakeUserCreated>();
            var events = new DomainEvent[] { created };
            var correlationId = Guid.NewGuid();
            RaiseEvents(_userId, events);

            // Act
            var now = DateTimeOffset.Now;
            await _sut.SaveEvents<FakeUser>(events, correlationId);

            // Assert
            string partitionKey = CorrelationTableEntity.GetPartitionKey(typeof(FakeUser), _userId);
            string rowKey = CorrelationTableEntity.GetRowKey(correlationId);
            var query = new TableQuery<CorrelationTableEntity>().Where($"PartitionKey eq '{partitionKey}' and RowKey eq '{rowKey}'");
            IEnumerable<CorrelationTableEntity> correlations = s_eventTable.ExecuteQuery(query);
            correlations.Should()
                .ContainSingle(e => e.CorrelationId == correlationId)
                .Which.HandledAt.ToLocalTime().Should().BeCloseTo(now, 1000);
        }

        [TestMethod]
        public void SaveEvents_does_not_fail_even_if_no_correlation()
        {
            var created = _fixture.Create<FakeUserCreated>();
            var events = new DomainEvent[] { created };
            RaiseEvents(_userId, events);

            Func<Task> action = () => _sut.SaveEvents<FakeUser>(events);

            action.ShouldNotThrow();
        }

        [TestMethod]
        public async Task SaveEvents_throws_DuplicateCorrelationException_if_correlation_duplicate()
        {
            var created = _fixture.Create<FakeUserCreated>();
            var usernameChanged = _fixture.Create<FakeUsernameChanged>();
            RaiseEvents(_userId, created, usernameChanged);
            var correlationId = Guid.NewGuid();
            await _sut.SaveEvents<FakeUser>(new[] { created }, correlationId);

            Func<Task> action = () => _sut.SaveEvents<FakeUser>(new[] { usernameChanged }, correlationId);

            action.ShouldThrow<DuplicateCorrelationException>().Where(
                x =>
                x.SourceType == typeof(FakeUser) &&
                x.SourceId == _userId &&
                x.CorrelationId == correlationId &&
                x.InnerException is StorageException);
        }

        [TestMethod]
        public async Task LoadEvents_restores_domain_events_correctly()
        {
            // Arrange
            var created = _fixture.Create<FakeUserCreated>();
            var usernameChanged = _fixture.Create<FakeUsernameChanged>();
            var events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(_userId, events);
            await _sut.SaveEvents<FakeUser>(events);

            // Act
            IEnumerable<IDomainEvent> actual = await _sut.LoadEvents<FakeUser>(_userId);

            // Assert
            actual.Should().BeInAscendingOrder(e => e.Version);
            actual.ShouldAllBeEquivalentTo(events);
        }

        [TestMethod]
        public async Task LoadEvents_correctly_restores_domain_events_after_specified_version()
        {
            // Arrange
            var created = _fixture.Create<FakeUserCreated>();
            var usernameChanged = _fixture.Create<FakeUsernameChanged>();
            var events = new DomainEvent[] { created, usernameChanged };
            RaiseEvents(_userId, events);
            await _sut.SaveEvents<FakeUser>(events);

            // Act
            IEnumerable<IDomainEvent> actual = await _sut.LoadEvents<FakeUser>(_userId, 1);

            // Assert
            actual.Should().BeInAscendingOrder(e => e.Version);
            actual.ShouldAllBeEquivalentTo(events.Skip(1));
        }

        private static void RaiseEvents(Guid sourceId, params DomainEvent[] events)
        {
            RaiseEvents(sourceId, 0, events);
        }

        private static void RaiseEvents(
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
