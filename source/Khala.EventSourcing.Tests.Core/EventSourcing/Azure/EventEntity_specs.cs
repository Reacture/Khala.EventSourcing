namespace Khala.EventSourcing.Azure
{
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EventEntity_specs
    {
        [TestMethod]
        public void sut_is_abstract()
        {
            typeof(EventEntity).IsAbstract.Should().BeTrue();
        }

        [TestMethod]
        public void sut_inherits_AggregateEntity()
        {
            typeof(EventEntity).BaseType.Should().Be(typeof(AggregateEntity));
        }
    }
}
