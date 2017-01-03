using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage.Table;

namespace ReactiveArchitecture.EventSourcing.Azure
{
    [TestClass]
    public class EventTableEntity_features
    {
        [TestMethod]
        public void EventTableEntity_inherits_TableEntity()
        {
            typeof(EventTableEntity).BaseType.Should().Be(typeof(TableEntity));
        }
    }
}
