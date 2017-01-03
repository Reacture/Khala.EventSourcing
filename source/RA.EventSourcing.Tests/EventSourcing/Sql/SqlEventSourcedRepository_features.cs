using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Ploeh.AutoFixture;
using Ploeh.AutoFixture.AutoMoq;
using Ploeh.AutoFixture.Idioms;
using Ploeh.AutoFixture.Xunit2;
using ReactiveArchitecture.FakeDomain;
using Xunit;

namespace ReactiveArchitecture.EventSourcing.Sql
{
    public class SqlEventSourcedRepository_features
    {
        [Fact]
        public void class_inherits_EventSourcedRepositoryT()
        {
            typeof(SqlEventSourcedRepository<>)
                .BaseType
                .GetGenericTypeDefinition()
                .Should().Be(typeof(EventSourcedRepository<>));
        }

        [Fact]
        public void sut_implements_ISqlEventSourcedRepository()
        {
            var sut = new SqlEventSourcedRepository<FakeUser>(
                Mock.Of<ISqlEventStore>(),
                Mock.Of<IEventPublisher>(),
                Mock.Of<Func<Guid, IEnumerable<IDomainEvent>, FakeUser>>());
            sut.Should().BeAssignableTo<ISqlEventSourcedRepository<FakeUser>>();
        }

        [Fact]
        public void class_has_guard_clauses()
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization());
            var assertion = new GuardClauseAssertion(fixture);
            assertion.Verify(typeof(SqlEventSourcedRepository<>));
        }

        [Theory]
        [AutoData]
        public async Task FindIdByUniqueIndexedProperty_relays_to_event_store(
            string name,
            string value,
            Guid? expected)
        {
            var eventStore = Mock.Of<ISqlEventStore>(
                x =>
                x.FindIdByUniqueIndexedProperty<FakeUser>(name, value) == Task.FromResult(expected));
            var sut = new SqlEventSourcedRepository<FakeUser>(
                eventStore,
                Mock.Of<IEventPublisher>(),
                Mock.Of<Func<Guid, IEnumerable<IDomainEvent>, FakeUser>>());

            Guid? actual = await sut.FindIdByUniqueIndexedProperty(name, value);

            actual.Should().Be(expected);
        }
    }
}
