namespace Khala.EventSourcing
{
    using System;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EventSourced_features
    {
        public abstract class AbstractAggregate : EventSourced
        {
            protected AbstractAggregate(Guid id)
                : base(id)
            {
            }

            public Guid Property { get; private set; }

            private void Handle(SomeDomainEvent domainEvent)
            {
                Property = domainEvent.Property;
            }
        }

        public class ConcreteAggregate : AbstractAggregate
        {
            public ConcreteAggregate(Guid id)
                : base(id)
            {
            }

            public void RaiseSomeDomainEvent(Guid value)
            {
                RaiseEvent(new SomeDomainEvent { Property = value });
            }
        }

        public class SomeDomainEvent : DomainEvent
        {
            public Guid Property { get; set; }
        }

        [TestMethod]
        public void sut_binds_domain_event_handlers_of_base_class_correctly()
        {
            // Arrange
            var sut = new ConcreteAggregate(Guid.NewGuid());
            Guid expected = Guid.NewGuid();

            // Act
            Action action = () => sut.RaiseSomeDomainEvent(expected);

            // Assert
            action.ShouldNotThrow();
            sut.Property.Should().Be(expected);
        }
    }
}
