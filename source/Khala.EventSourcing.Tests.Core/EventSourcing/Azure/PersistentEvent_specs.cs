namespace Khala.EventSourcing.Azure
{
    using System;
    using System.Reflection;
    using AutoFixture;
    using AutoFixture.AutoMoq;
    using AutoFixture.Idioms;
    using FluentAssertions;
    using Khala.Messaging;
    using Xunit;
    using Xunit.Abstractions;

    public class PersistentEvent_specs
    {
        private readonly ITestOutputHelper _output;

        public PersistentEvent_specs(ITestOutputHelper output)
        {
            _output = output;
        }

        public class SomeDomainEvent : DomainEvent
        {
            public int Foo { get; set; }

            public string Bar { get; set; }
        }

        [Fact]
        public void sut_inherits_EventEntity()
        {
            typeof(PersistentEvent).BaseType.Should().Be(typeof(EventEntity));
        }

        [Fact]
        public void GetRowKey_returns_formatted_version()
        {
            int version = new Fixture().Create<int>();
            string actual = PersistentEvent.GetRowKey(version);
            actual.Should().Be($"{version:D10}");
        }

        [Fact]
        public void Create_returns_PersistentEvent_instance()
        {
            IFixture fixture = new Fixture();
            fixture.Register<IDomainEvent>(() => fixture.Create<SomeDomainEvent>());

            var actual = PersistentEvent.Create(
                fixture.Create<Type>(),
                fixture.Create<Envelope<IDomainEvent>>(),
                new JsonMessageSerializer());

            actual.Should().NotBeNull();
        }

        [Fact]
        public void Create_has_guard_clauses()
        {
            MethodInfo mut = typeof(PersistentEvent).GetMethod("Create");
            IFixture builder = new Fixture().Customize(new AutoMoqCustomization());
            new GuardClauseAssertion(builder).Verify(mut);
        }

        [Fact]
        public void Create_sets_PartitionKey_correctly()
        {
            IFixture fixture = new Fixture();
            Type aggregateType = fixture.Create<Type>();
            SomeDomainEvent domainEvent = fixture.Create<SomeDomainEvent>();
            _output.WriteLine($"SourceId: {domainEvent.SourceId}");
            fixture.Inject<IDomainEvent>(domainEvent);

            var actual = PersistentEvent.Create(
                aggregateType,
                fixture.Create<Envelope<IDomainEvent>>(),
                new JsonMessageSerializer());

            actual.PartitionKey.Should().Be(AggregateEntity.GetPartitionKey(aggregateType, domainEvent.SourceId));
        }

        [Fact]
        public void Create_sets_RowKey_correctly()
        {
            IFixture fixture = new Fixture();
            SomeDomainEvent domainEvent = fixture.Create<SomeDomainEvent>();
            _output.WriteLine($"Version: {domainEvent.Version}");
            fixture.Inject<IDomainEvent>(domainEvent);

            var actual = PersistentEvent.Create(
                fixture.Create<Type>(),
                fixture.Create<Envelope<IDomainEvent>>(),
                new JsonMessageSerializer());

            actual.RowKey.Should().Be(PersistentEvent.GetRowKey(domainEvent.Version));
        }

        [Fact]
        public void Create_sets_Version_correctly()
        {
            IFixture fixture = new Fixture();
            SomeDomainEvent domainEvent = fixture.Create<SomeDomainEvent>();
            _output.WriteLine($"Version: {domainEvent.Version}");
            fixture.Inject<IDomainEvent>(domainEvent);

            var actual = PersistentEvent.Create(
                fixture.Create<Type>(),
                fixture.Create<Envelope<IDomainEvent>>(),
                new JsonMessageSerializer());

            actual.Version.Should().Be(domainEvent.Version);
        }

        [Fact]
        public void Create_sets_EventType_correctly()
        {
            IFixture fixture = new Fixture();
            SomeDomainEvent domainEvent = fixture.Create<SomeDomainEvent>();
            fixture.Inject<IDomainEvent>(domainEvent);

            var actual = PersistentEvent.Create(
                fixture.Create<Type>(),
                fixture.Create<Envelope<IDomainEvent>>(),
                new JsonMessageSerializer());

            actual.EventType.Should().Be(typeof(SomeDomainEvent).FullName);
        }

        [Fact]
        public void Create_sets_RaisedAt_correctly()
        {
            IFixture fixture = new Fixture();
            SomeDomainEvent domainEvent = fixture.Create<SomeDomainEvent>();
            fixture.Inject<IDomainEvent>(domainEvent);
            _output.WriteLine($"RaisedAt: {domainEvent.RaisedAt}");

            var actual = PersistentEvent.Create(
                fixture.Create<Type>(),
                fixture.Create<Envelope<IDomainEvent>>(),
                new JsonMessageSerializer());

            actual.RaisedAt.Should().Be(domainEvent.RaisedAt);
        }

        [Fact]
        public void Create_sets_MessageId_correctly()
        {
            IFixture fixture = new Fixture();
            SomeDomainEvent domainEvent = fixture.Create<SomeDomainEvent>();
            fixture.Inject<IDomainEvent>(domainEvent);
            Envelope<IDomainEvent> envelope = fixture.Create<Envelope<IDomainEvent>>();
            _output.WriteLine($"MessageId: {envelope.MessageId}");

            var actual = PersistentEvent.Create(
                fixture.Create<Type>(),
                envelope,
                new JsonMessageSerializer());

            actual.MessageId.Should().Be(envelope.MessageId);
        }

        [Fact]
        public void Create_sets_EventJson_correctly()
        {
            IFixture fixture = new Fixture();
            SomeDomainEvent domainEvent = fixture.Create<SomeDomainEvent>();
            fixture.Inject<IDomainEvent>(domainEvent);
            var serializer = new JsonMessageSerializer();

            var actual = PersistentEvent.Create(
                fixture.Create<Type>(),
                fixture.Create<Envelope<IDomainEvent>>(),
                serializer);

            object restored = serializer.Deserialize(actual.EventJson);
            restored.Should().BeOfType<SomeDomainEvent>();
            restored.ShouldBeEquivalentTo(domainEvent);
        }

        [Fact]
        public void Create_sets_OperationId_correctly()
        {
            IFixture fixture = new Fixture();
            SomeDomainEvent domainEvent = fixture.Create<SomeDomainEvent>();
            fixture.Inject<IDomainEvent>(domainEvent);
            Envelope<IDomainEvent> envelope = fixture.Create<Envelope<IDomainEvent>>();
            _output.WriteLine($"OperationId: {envelope.OperationId}");

            var actual = PersistentEvent.Create(
                fixture.Create<Type>(),
                envelope,
                new JsonMessageSerializer());

            actual.OperationId.Should().Be(envelope.OperationId);
        }

        [Fact]
        public void Create_sets_CorrelationId_correctly()
        {
            IFixture fixture = new Fixture();
            SomeDomainEvent domainEvent = fixture.Create<SomeDomainEvent>();
            fixture.Inject<IDomainEvent>(domainEvent);
            Envelope<IDomainEvent> envelope = fixture.Create<Envelope<IDomainEvent>>();
            _output.WriteLine($"CorrelationId: {envelope.CorrelationId}");

            var actual = PersistentEvent.Create(
                fixture.Create<Type>(),
                envelope,
                new JsonMessageSerializer());

            actual.CorrelationId.Should().Be(envelope.CorrelationId);
        }

        [Fact]
        public void Create_sets_Contributor_correctly()
        {
            IFixture fixture = new Fixture();
            SomeDomainEvent domainEvent = fixture.Create<SomeDomainEvent>();
            fixture.Inject<IDomainEvent>(domainEvent);
            Envelope<IDomainEvent> envelope = fixture.Create<Envelope<IDomainEvent>>();
            _output.WriteLine($"Contributor: {envelope.Contributor}");

            var actual = PersistentEvent.Create(
                fixture.Create<Type>(),
                envelope,
                new JsonMessageSerializer());

            actual.Contributor.Should().Be(envelope.Contributor);
        }
    }
}
