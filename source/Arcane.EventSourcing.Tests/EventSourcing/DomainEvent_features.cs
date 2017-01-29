using System;
using System.Reflection;
using Arcane.Messaging;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Ploeh.AutoFixture;
using Ploeh.AutoFixture.AutoMoq;
using Ploeh.AutoFixture.Idioms;

namespace Arcane.EventSourcing
{
    [TestClass]
    public class DomainEvent_features
    {
        private IFixture fixture =
            new Fixture().Customize(new AutoMoqCustomization());

        public class FakeDomainEvent : DomainEvent
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
            var sut = fixture.Create<FakeDomainEvent>();
            string actual = sut.PartitionKey;
            actual.Should().Be(sut.SourceId.ToString());
        }

        [TestMethod]
        public void Raise_has_guard_clause()
        {
            var assertion = new GuardClauseAssertion(fixture);
            fixture.Register<FakeDomainEvent, DomainEvent>(evt => evt);
            assertion.Verify(typeof(DomainEvent).GetMethod("Raise"));
        }

        [TestMethod]
        public void Raise_sets_SourceId_correctly()
        {
            var sourceId = Guid.NewGuid();
            var versionedEntity =
                Mock.Of<IVersionedEntity>(x => x.Id == sourceId);
            var sut = new FakeDomainEvent();

            sut.Raise(versionedEntity);

            sut.SourceId.Should().Be(sourceId);
        }

        [TestMethod]
        public void Raise_sets_version_correctly()
        {
            var version = fixture.Create<int>();
            var versionedEntity =
                Mock.Of<IVersionedEntity>(x => x.Version == version);
            var sut = new FakeDomainEvent();

            sut.Raise(versionedEntity);

            sut.Version.Should().Be(version + 1);
        }

        [TestMethod]
        public void Raise_sets_RaisedAt_correctly()
        {
            var versionedEntity = Mock.Of<IVersionedEntity>();
            var sut = new FakeDomainEvent();

            sut.Raise(versionedEntity);

            sut.RaisedAt.Should().BeCloseTo(DateTimeOffset.Now);
        }
    }
}
