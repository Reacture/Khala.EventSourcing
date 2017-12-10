namespace Khala.EventSourcing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Khala.FakeDomain;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Ploeh.AutoFixture;

    [TestClass]
    public class EventSourcingExtensions_specs
    {
        [TestMethod]
        public void SaveAndPublish_relays_with_null_correlation_and_null_contributor_and_none_cancellation_token()
        {
            var task = Task.FromResult(true);
            var source = new FakeUser(Guid.NewGuid(), "foo");
            var repository = Mock.Of<IEventSourcedRepository<FakeUser>>(
                x => x.SaveAndPublish(source, default, default, CancellationToken.None) == task);

            Task result = repository.SaveAndPublish(source);

            Mock.Get(repository).Verify(
                x =>
                x.SaveAndPublish(source, default, default, CancellationToken.None),
                Times.Once());
            result.Should().BeSameAs(task);
        }

        [TestMethod]
        public void SaveAndPublish_relays_with_null_correlation_and_null_contributor()
        {
            var fixture = new Fixture();
            var repository = Mock.Of<IEventSourcedRepository<FakeUser>>();
            var source = fixture.Create<FakeUser>();

            repository.SaveAndPublish(source, CancellationToken.None);

            Mock.Get(repository).Verify(
                x =>
                x.SaveAndPublish(source, default, default, CancellationToken.None),
                Times.Once());
        }

        [TestMethod]
        public void SaveAndPublish_relays_with_null_contributor_and_none_cancellation_token()
        {
            var task = Task.FromResult(true);
            var source = new FakeUser(Guid.NewGuid(), "foo");
            var correlationId = Guid.NewGuid();
            var repository = Mock.Of<IEventSourcedRepository<FakeUser>>(
                x => x.SaveAndPublish(source, correlationId, default, CancellationToken.None) == task);

            Task result = repository.SaveAndPublish(source, correlationId);

            Mock.Get(repository).Verify(
                x =>
                x.SaveAndPublish(source, correlationId, default, CancellationToken.None),
                Times.Once());
            result.Should().BeSameAs(task);
        }

        [TestMethod]
        public void SaveAndPublish_relays_with_null_contributor()
        {
            var task = Task.FromResult(true);
            var source = new FakeUser(Guid.NewGuid(), "foo");
            var correlationId = Guid.NewGuid();
            var cancellation = new CancellationTokenSource();
            var cancellationToken = cancellation.Token;
            var repository = Mock.Of<IEventSourcedRepository<FakeUser>>(
                x => x.SaveAndPublish(source, correlationId, default, cancellationToken) == task);

            Task result = repository.SaveAndPublish(source, correlationId, cancellationToken);

            Mock.Get(repository).Verify(
                x =>
                x.SaveAndPublish(source, correlationId, default, cancellationToken),
                Times.Once());
            result.Should().BeSameAs(task);
        }

        [TestMethod]
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
    }
}
