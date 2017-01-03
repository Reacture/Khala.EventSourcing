using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ploeh.AutoFixture;
using Ploeh.AutoFixture.AutoMoq;
using Ploeh.AutoFixture.Idioms;

namespace ReactiveArchitecture.EventSourcing.Azure
{
    [TestClass]
    public class AzureEventStore_features
    {
        private AzureEventStore sut;

        [TestInitialize]
        public void TestInitialize()
        {
            sut = new AzureEventStore();
        }

        [TestMethod]
        public void sut_implements_IEventStore()
        {
            sut.Should().BeAssignableTo<IEventStore>();
        }

        [TestMethod]
        public void class_has_guard_clauses()
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization());
            var assertion = new GuardClauseAssertion(fixture);
            assertion.Verify(typeof(AzureEventStore));
        }
    }
}
