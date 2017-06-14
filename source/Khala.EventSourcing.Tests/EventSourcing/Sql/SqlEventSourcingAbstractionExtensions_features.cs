namespace Khala.EventSourcing.Sql
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
    public class SqlEventSourcingAbstractionExtensions_features
    {
        [TestMethod]
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
