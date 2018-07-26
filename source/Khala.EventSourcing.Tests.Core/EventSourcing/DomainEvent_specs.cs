namespace Khala.EventSourcing
{
    using System;
    using System.Reflection;
    using AutoFixture;
    using AutoFixture.AutoMoq;
    using AutoFixture.Idioms;
    using FluentAssertions;
    using Khala.Messaging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    public class DomainEvent_specs
    {
        private IFixture _fixture =
            new Fixture().Customize(new AutoMoqCustomization());

        private class FakeDomainEvent : DomainEvent
        {
        }

        [TestMethod]
        public void class_is_abstract()
        {
            typeof(DomainEvent).IsAbstract.Should().BeTrue();
        }

        [TestMethod]
        public void DomainEvent_implements_IDomainEvent()
        {
            var sut = new FakeDomainEvent();
            sut.Should().BeAssignableTo<IDomainEvent>();
        }

        [TestMethod]
        public void DomainEvent_implements_IPartitioned()
        {
            var sut = new FakeDomainEvent();
            sut.Should().BeAssignableTo<IPartitioned>();
        }

        [TestMethod]
        public void PartitionKey_is_decorated_with_JsonIgnore()
        {
            PropertyInfo property = typeof(DomainEvent).GetProperty("PartitionKey");
            property.Should().BeDecoratedWith<JsonIgnoreAttribute>();
        }

        [TestMethod]
        public void PartitionKey_returns_SourceId_as_string()
        {
            FakeDomainEvent sut = _fixture.Create<FakeDomainEvent>();
            string actual = sut.PartitionKey;
            actual.Should().Be(sut.SourceId.ToString());
        }

        [TestMethod]
        public void Raise_has_guard_clause()
        {
            var assertion = new GuardClauseAssertion(_fixture);
            _fixture.Register<FakeDomainEvent, DomainEvent>(evt => evt);
            assertion.Verify(typeof(DomainEvent).GetMethod("Raise"));
        }

        [TestMethod]
        public void Raise_sets_SourceId_correctly()
        {
            var sourceId = Guid.NewGuid();
            IVersionedEntity versionedEntity =
                Mock.Of<IVersionedEntity>(x => x.Id == sourceId);
            var sut = new FakeDomainEvent();

            sut.Raise(versionedEntity);

            sut.SourceId.Should().Be(sourceId);
        }

        [TestMethod]
        public void Raise_sets_version_correctly()
        {
            int version = _fixture.Create<int>();
            IVersionedEntity versionedEntity =
                Mock.Of<IVersionedEntity>(x => x.Version == version);
            var sut = new FakeDomainEvent();

            sut.Raise(versionedEntity);

            sut.Version.Should().Be(version + 1);
        }

        [TestMethod]
        public void Raise_sets_RaisedAt_correctly()
        {
            IVersionedEntity versionedEntity = Mock.Of<IVersionedEntity>();
            var sut = new FakeDomainEvent();

            sut.Raise(versionedEntity);

            sut.RaisedAt.Kind.Should().Be(DateTimeKind.Utc);
            sut.RaisedAt.Should().BeCloseTo(DateTime.UtcNow);
        }

        [TestMethod]
        public void PartitionKey_is_virtual()
        {
            PropertyInfo property = typeof(DomainEvent).GetProperty("PartitionKey");
            bool isVirtual = property.GetGetMethod().IsVirtual;
            isVirtual.Should().BeTrue();
        }
    }
}
