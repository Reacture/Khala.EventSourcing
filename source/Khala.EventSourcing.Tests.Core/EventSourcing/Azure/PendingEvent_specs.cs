namespace Khala.EventSourcing.Azure
{
    using System;
    using System.Reflection;
    using AutoFixture;
    using AutoFixture.AutoMoq;
    using AutoFixture.Idioms;
    using FluentAssertions;
    using Khala.Messaging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PendingEvent_specs
    {
        public class SomeDomainEvent : DomainEvent
        {
            public int Foo { get; set; }

            public string Bar { get; set; }
        }

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void sut_inherits_EventEntity()
        {
            typeof(PendingEvent).BaseType.Should().Be(typeof(EventEntity));
        }

        [TestMethod]
        public void GetRowKey_returns_prefixed_formatted_version()
        {
            int version = new Fixture().Create<int>();
            string actual = PendingEvent.GetRowKey(version);
            actual.Should().Be($"Pending-{version:D10}");
        }

        [TestMethod]
        public void Create_returns_PersistentEvent_instance()
        {
            IFixture fixture = new Fixture();
            fixture.Register<IDomainEvent>(() => fixture.Create<SomeDomainEvent>());

            var actual = PendingEvent.Create(
                fixture.Create<Type>(),
                fixture.Create<Envelope<IDomainEvent>>(),
                new JsonMessageSerializer());

            actual.Should().NotBeNull();
        }

        [TestMethod]
        public void Create_has_guard_clauses()
        {
            MethodInfo mut = typeof(PendingEvent).GetMethod("Create");
            IFixture builder = new Fixture().Customize(new AutoMoqCustomization());
            new GuardClauseAssertion(builder).Verify(mut);
        }

        [TestMethod]
        public void Create_sets_PartitionKey_correctly()
        {
            IFixture fixture = new Fixture();
            Type sourceType = fixture.Create<Type>();
            SomeDomainEvent domainEvent = fixture.Create<SomeDomainEvent>();
            TestContext.WriteLine($"SourceId: {domainEvent.SourceId}");
            fixture.Inject<IDomainEvent>(domainEvent);

            var actual = PendingEvent.Create(
                sourceType,
                fixture.Create<Envelope<IDomainEvent>>(),
                new JsonMessageSerializer());

            actual.PartitionKey.Should().Be(AggregateEntity.GetPartitionKey(sourceType, domainEvent.SourceId));
        }

        [TestMethod]
        public void Create_sets_RowKey_correctly()
        {
            IFixture fixture = new Fixture();
            SomeDomainEvent domainEvent = fixture.Create<SomeDomainEvent>();
            TestContext.WriteLine($"Version: {domainEvent.Version}");
            fixture.Inject<IDomainEvent>(domainEvent);

            var actual = PendingEvent.Create(
                fixture.Create<Type>(),
                fixture.Create<Envelope<IDomainEvent>>(),
                new JsonMessageSerializer());

            actual.RowKey.Should().Be(PendingEvent.GetRowKey(domainEvent.Version));
        }

        [TestMethod]
        public void Create_sets_MessageId_correctly()
        {
            IFixture fixture = new Fixture();
            SomeDomainEvent domainEvent = fixture.Create<SomeDomainEvent>();
            fixture.Inject<IDomainEvent>(domainEvent);
            Envelope<IDomainEvent> envelope = fixture.Create<Envelope<IDomainEvent>>();
            TestContext.WriteLine($"MessageId: {envelope.MessageId}");

            var actual = PendingEvent.Create(
                fixture.Create<Type>(),
                envelope,
                new JsonMessageSerializer());

            actual.MessageId.Should().Be(envelope.MessageId);
        }

        [TestMethod]
        public void Create_sets_EventJson_correctly()
        {
            IFixture fixture = new Fixture();
            SomeDomainEvent domainEvent = fixture.Create<SomeDomainEvent>();
            fixture.Inject<IDomainEvent>(domainEvent);
            var serializer = new JsonMessageSerializer();

            var actual = PendingEvent.Create(
                fixture.Create<Type>(),
                fixture.Create<Envelope<IDomainEvent>>(),
                serializer);

            object restored = serializer.Deserialize(actual.EventJson);
            restored.Should().BeOfType<SomeDomainEvent>();
            restored.ShouldBeEquivalentTo(domainEvent);
        }

        [TestMethod]
        public void Create_sets_OperationId_correctly()
        {
            IFixture fixture = new Fixture();
            SomeDomainEvent domainEvent = fixture.Create<SomeDomainEvent>();
            fixture.Inject<IDomainEvent>(domainEvent);
            Envelope<IDomainEvent> envelope = fixture.Create<Envelope<IDomainEvent>>();
            TestContext.WriteLine($"OperationId: {envelope.OperationId}");

            var actual = PendingEvent.Create(
                fixture.Create<Type>(),
                envelope,
                new JsonMessageSerializer());

            actual.OperationId.Should().Be(envelope.OperationId);
        }

        [TestMethod]
        public void Create_sets_CorrelationId_correctly()
        {
            IFixture fixture = new Fixture();
            SomeDomainEvent domainEvent = fixture.Create<SomeDomainEvent>();
            fixture.Inject<IDomainEvent>(domainEvent);
            Envelope<IDomainEvent> envelope = fixture.Create<Envelope<IDomainEvent>>();
            TestContext.WriteLine($"CorrelationId: {envelope.CorrelationId}");

            var actual = PendingEvent.Create(
                fixture.Create<Type>(),
                envelope,
                new JsonMessageSerializer());

            actual.CorrelationId.Should().Be(envelope.CorrelationId);
        }

        [TestMethod]
        public void Create_sets_Contributor_correctly()
        {
            IFixture fixture = new Fixture();
            SomeDomainEvent domainEvent = fixture.Create<SomeDomainEvent>();
            fixture.Inject<IDomainEvent>(domainEvent);
            Envelope<IDomainEvent> envelope = fixture.Create<Envelope<IDomainEvent>>();
            TestContext.WriteLine($"Contributor: {envelope.Contributor}");

            var actual = PendingEvent.Create(
                fixture.Create<Type>(),
                envelope,
                new JsonMessageSerializer());

            actual.Contributor.Should().Be(envelope.Contributor);
        }
    }
}
