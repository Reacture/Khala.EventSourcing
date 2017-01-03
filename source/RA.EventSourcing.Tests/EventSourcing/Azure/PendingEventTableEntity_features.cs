using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage.Table;

namespace ReactiveArchitecture.EventSourcing.Azure
{
    [TestClass]
    public class PendingEventTableEntity_features
    {
        [TestMethod]
        public void PendingEventEntity_inherits_TableEntity()
        {
            typeof(PendingEventTableEntity).BaseType.Should().Be(typeof(TableEntity));
        }
    }
}
