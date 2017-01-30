using System;
using System.Threading;
using System.Threading.Tasks;
using Arcane.FakeDomain;
using FluentAssertions;
using Moq;
using Ploeh.AutoFixture;
using Xunit;

namespace Arcane.EventSourcing.Sql
{
    public class SqlEventSourcingAbstractionExtensions_features
    {
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
