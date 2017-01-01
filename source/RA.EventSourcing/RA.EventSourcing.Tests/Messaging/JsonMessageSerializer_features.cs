namespace ReactiveArchitecture.Messaging
{
    using System;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Ploeh.AutoFixture;
    using Ploeh.AutoFixture.Idioms;

    [TestClass]
    public class JsonMessageSerializer_features
    {
        private IFixture fixture = new Fixture();

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void Deserialize_has_guard_clause()
        {
            var assertion = new GuardClauseAssertion(fixture);
            assertion.Verify(typeof(JsonMessageSerializer).GetMethod(
                nameof(JsonMessageSerializer.Deserialize)));
        }

        public class MutableMessage
        {
            public Guid GuidProp { get; set; }

            public int Int32Prop { get; set; }

            public double DoubleProp { get; set; }

            public string StringProp { get; set; }
        }

        [TestMethod]
        public void Deserialize_restores_mutable_message_correctly()
        {
            // Arrange
            var sut = new JsonMessageSerializer();
            var message = fixture.Create<MutableMessage>();
            string serialized = sut.Serialize(message);
            TestContext.WriteLine("{0}", serialized);

            // Act
            object actual = sut.Deserialize(serialized);

            // Assert
            actual.Should().BeOfType<MutableMessage>();
            actual.ShouldBeEquivalentTo(message);
        }
    }
}
