using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Arcane.FakeDomain;
using FluentAssertions;
using Moq;
using Ploeh.AutoFixture;
using Ploeh.AutoFixture.AutoMoq;
using Xunit;

namespace Arcane.EventSourcing.Sql
{
    public class SqlEventSourcingExtensions_features
    {
        [Fact]
        public void SaveEvents_relays_with_null_correlaton()
        {
            var task = Task.FromResult(true);
            var events = new IDomainEvent[] { };
            var cancellation = new CancellationTokenSource();
            var cancellationToken = cancellation.Token;
            var eventStore = Mock.Of<ISqlEventStore>(
                x => x.SaveEvents<FakeUser>(events, null, cancellationToken) == task);

            Task result = eventStore.SaveEvents<FakeUser>(events, cancellationToken);

            Mock.Get(eventStore).Verify(
                x =>
                x.SaveEvents<FakeUser>(events, null, cancellationToken),
                Times.Once());
            result.Should().BeSameAs(task);
        }

        [Fact]
        public void SaveEvents_relays_with_none_cancellation_token()
        {
            var task = Task.FromResult(true);
            var events = new IDomainEvent[] { };
            var correlationId = Guid.NewGuid();
            var eventStore = Mock.Of<ISqlEventStore>(
                x => x.SaveEvents<FakeUser>(events, correlationId, CancellationToken.None) == task);

            Task result = eventStore.SaveEvents<FakeUser>(events, correlationId);

            Mock.Get(eventStore).Verify(
                x =>
                x.SaveEvents<FakeUser>(events, correlationId, CancellationToken.None),
                Times.Once());
            result.Should().BeSameAs(task);
        }

        [Fact]
        public void SaveEvents_relays_with_null_correlation_and_none_cancellation_token()
        {
            var task = Task.FromResult(true);
            var events = new IDomainEvent[] { };
            var eventStore = Mock.Of<ISqlEventStore>(
                x => x.SaveEvents<FakeUser>(events, null, CancellationToken.None) == task);

            Task result = eventStore.SaveEvents<FakeUser>(events);

            Mock.Get(eventStore).Verify(
                x =>
                x.SaveEvents<FakeUser>(events, null, CancellationToken.None),
                Times.Once());
            result.Should().BeSameAs(task);
        }

        [Fact]
        public void LoadEvents_relays_with_default_version()
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization());
            var task = fixture.Create<Task<IEnumerable<IDomainEvent>>>();
            var sourceId = Guid.NewGuid();
            var cancellation = new CancellationTokenSource();
            var cancellationToken = cancellation.Token;
            var eventStore = Mock.Of<ISqlEventStore>(
                x => x.LoadEvents<FakeUser>(sourceId, default(int), cancellationToken) == task);

            Task<IEnumerable<IDomainEvent>> result =
                eventStore.LoadEvents<FakeUser>(sourceId, cancellationToken);

            Mock.Get(eventStore).Verify(
                x =>
                x.LoadEvents<FakeUser>(sourceId, default(int), cancellationToken),
                Times.Once());
            result.Should().BeSameAs(task);
        }

        [Fact]
        public void LoadEvents_relays_with_none_cancellation_token()
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization());
            var task = fixture.Create<Task<IEnumerable<IDomainEvent>>>();
            var sourceId = Guid.NewGuid();
            var afterVersion = fixture.Create<int>();
            var eventStore = Mock.Of<ISqlEventStore>(
                x => x.LoadEvents<FakeUser>(sourceId, afterVersion, CancellationToken.None) == task);

            Task<IEnumerable<IDomainEvent>> result =
                eventStore.LoadEvents<FakeUser>(sourceId, afterVersion);

            Mock.Get(eventStore).Verify(
                x =>
                x.LoadEvents<FakeUser>(sourceId, afterVersion, CancellationToken.None),
                Times.Once());
            result.Should().BeSameAs(task);
        }

        [Fact]
        public void LoadEvents_relays_with_default_version_and_none_cancellation_token()
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization());
            var task = fixture.Create<Task<IEnumerable<IDomainEvent>>>();
            var sourceId = Guid.NewGuid();
            var eventStore = Mock.Of<ISqlEventStore>(
                x => x.LoadEvents<FakeUser>(sourceId, default(int), CancellationToken.None) == task);

            Task<IEnumerable<IDomainEvent>> result =
                eventStore.LoadEvents<FakeUser>(sourceId);

            Mock.Get(eventStore).Verify(
                x =>
                x.LoadEvents<FakeUser>(sourceId, default(int), CancellationToken.None),
                Times.Once());
            result.Should().BeSameAs(task);
        }

        [Fact]
        public void FindIdByUniqueIndexedProperty_relays_with_none_cancellation_token()
        {
            var fixture = new Fixture();
            var task = fixture.Create<Task<Guid?>>();
            string name = fixture.Create(nameof(name));
            string value = fixture.Create(nameof(value));
            var eventStore = Mock.Of<ISqlEventStore>(
                x => x.FindIdByUniqueIndexedProperty<FakeUser>(name, value, CancellationToken.None) == task);

            Task<Guid?> result = eventStore.FindIdByUniqueIndexedProperty<FakeUser>(name, value);

            Mock.Get(eventStore).Verify(
                x =>
                x.FindIdByUniqueIndexedProperty<FakeUser>(name, value, CancellationToken.None),
                Times.Once());
            result.Should().BeSameAs(task);
        }
    }
}
