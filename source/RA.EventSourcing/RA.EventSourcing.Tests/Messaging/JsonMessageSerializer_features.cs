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

        public class ImmutableMessage
        {
            public ImmutableMessage(
                Guid guidProp,
                int int32Prop,
                double doubleProp,
                string stringProp)
            {
                GuidProp = guidProp;
                Int32Prop = int32Prop;
                DoubleProp = doubleProp;
                StringProp = stringProp;
            }

            public Guid GuidProp { get; }

            public int Int32Prop { get; }

            public double DoubleProp { get; }

            public string StringProp { get; }
        }

        [TestMethod]
        public void Deserialize_restores_immutable_message_correctly()
        {
            // Arrange
            var sut = new JsonMessageSerializer();
            var message = fixture.Create<ImmutableMessage>();
            string serialized = sut.Serialize(message);
            TestContext.WriteLine("{0}", serialized);

            // Act
            object actual = sut.Deserialize(serialized);

            // Assert
            actual.Should().BeOfType<ImmutableMessage>();
            actual.ShouldBeEquivalentTo(message);
        }

        [TestMethod]
        public void Deserialize_restores_message_of_unknown_type_to_dynamic()
        {
            // Arrange
            string stringProp = $"{Guid.NewGuid()}";
            var json = $@"
{{
  ""$type"": ""UnknownNamespace.UnknownMessage, UnknownAssembly"",
  ""StringProp"": ""{stringProp}""
}}";
            var sut = new JsonMessageSerializer();
            var actual = default(object);

            // Act
            Action action = () => actual = sut.Deserialize(json);

            // Assert
            action.ShouldNotThrow();
            actual.Should().NotBeNull();
            Assert.AreEqual((string)((dynamic)actual).StringProp, stringProp);
        }

        [TestMethod]
        public void Deserialize_restores_untyped_message_to_dynamic()
        {
            // Arrange
            string stringProp = $"{Guid.NewGuid()}";
            var json = $@"{{ ""StringProp"": ""{stringProp}"" }}";
            var sut = new JsonMessageSerializer();
            var actual = default(object);

            // Act
            Action action = () => actual = sut.Deserialize(json);

            // Assert
            action.ShouldNotThrow();
            actual.Should().NotBeNull();
            Assert.AreEqual((string)((dynamic)actual).StringProp, stringProp);
        }
    }
}
