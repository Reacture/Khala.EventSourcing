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
    using Ploeh.AutoFixture.AutoMoq;
    using Ploeh.AutoFixture.Idioms;

    [TestClass]
    public class SqlEventSourcingAbstractionExtensions_specs
    {
        [TestMethod]
        public void sut_has_guard_clauses()
        {
            var builder = new Fixture().Customize(new AutoMoqCustomization());
            new GuardClauseAssertion(builder).Verify(typeof(SqlEventSourcingAbstractionExtensions));
        }

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
