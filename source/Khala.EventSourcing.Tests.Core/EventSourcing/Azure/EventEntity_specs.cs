namespace Khala.EventSourcing.Azure
{
    using FluentAssertions;
    using Xunit;

    public class EventEntity_specs
    {
        [Fact]
        public void sut_is_abstract()
        {
            typeof(EventEntity).IsAbstract.Should().BeTrue();
        }

        [Fact]
        public void sut_inherits_AggregateEntity()
        {
            typeof(EventEntity).BaseType.Should().Be(typeof(AggregateEntity));
        }
    }
}
