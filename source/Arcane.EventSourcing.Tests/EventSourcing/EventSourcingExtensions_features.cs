using System;
using System.Threading;
using System.Threading.Tasks;
using Arcane.EventSourcing.Sql;
using Arcane.FakeDomain;
using FluentAssertions;
using Moq;
using Ploeh.AutoFixture;
using Ploeh.AutoFixture.AutoMoq;
using Ploeh.AutoFixture.Idioms;
using Xunit;

namespace Arcane.EventSourcing
{
    public class EventSourcingExtensions_features
    {
        [Fact]
        public void class_has_guard_clauses()
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization());
            var assertion = new GuardClauseAssertion(fixture);
            assertion.Verify(typeof(EventSourcingExtensions));
        }

        [Fact]
        public void Save_relays_with_null_correlation()
        {
            var fixture = new Fixture();
            var repository = Mock.Of<IEventSourcedRepository<FakeUser>>();
            var source = fixture.Create<FakeUser>();

            repository.Save(source, CancellationToken.None);

            Mock.Get(repository).Verify(
                x =>
                x.Save(source, null, CancellationToken.None),
                Times.Once());
        }

        [Fact]
        public void Save_relays_with_none_cancellation_token()
        {
            var task = Task.FromResult(true);
            var source = new FakeUser(Guid.NewGuid(), "foo");
            var correlationId = Guid.NewGuid();
            var repository = Mock.Of<IEventSourcedRepository<FakeUser>>(
                x => x.Save(source, correlationId, CancellationToken.None) == task);

            Task result = repository.Save(source, correlationId);

            Mock.Get(repository).Verify(
                x =>
                x.Save(source, correlationId, CancellationToken.None),
                Times.Once());
            result.Should().BeSameAs(task);
        }

        [Fact]
        public void Save_relays_with_null_correlation_and_none_cancellation_token()
        {
            var task = Task.FromResult(true);
            var source = new FakeUser(Guid.NewGuid(), "foo");
            var repository = Mock.Of<IEventSourcedRepository<FakeUser>>(
                x => x.Save(source, null, CancellationToken.None) == task);

            Task result = repository.Save(source);

            Mock.Get(repository).Verify(
                x =>
                x.Save(source, null, CancellationToken.None),
                Times.Once());
            result.Should().BeSameAs(task);
        }

        [Fact]
        public void Find_relays_with_none_cancellation_token()
        {
            var source = new FakeUser(Guid.NewGuid(), "foo");
            var task = Task.FromResult(source);
            var repository = Mock.Of<IEventSourcedRepository<FakeUser>>(
                x => x.Find(source.Id, CancellationToken.None) == task);

            Task<FakeUser> result = repository.Find(source.Id);

            Mock.Get(repository).Verify(
                x => x.Find(source.Id, CancellationToken.None), Times.Once());
            result.Should().BeSameAs(task);
        }

        [Fact]
        public void FindIdByUniqueIndexedProperty_relays_with_none_cancellation_token()
        {
            var fixture = new Fixture();
            var task = fixture.Create<Task<Guid?>>();
            string name = fixture.Create(nameof(name));
            string value = fixture.Create(nameof(value));
            var repository = Mock.Of<ISqlEventSourcedRepository<FakeUser>>(
                x =>
                x.FindIdByUniqueIndexedProperty(name, value, CancellationToken.None) == task);

            Task<Guid?> result = repository.FindIdByUniqueIndexedProperty(name, value);

            Mock.Get(repository).Verify(
                x =>
                x.FindIdByUniqueIndexedProperty(name, value, CancellationToken.None),
                Times.Once());
            result.Should().BeSameAs(task);
        }
    }
}
