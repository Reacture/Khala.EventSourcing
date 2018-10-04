namespace Khala.EventSourcing.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class UniqueIndexedProperty_specs
    {
        [TestMethod]
        public void sut_has_AggregateType_property()
        {
            typeof(UniqueIndexedProperty).Should().HaveProperty<string>("AggregateType");
        }

        [TestMethod]
        public void AggregateType_is_decorated_with_StringLength()
        {
            typeof(UniqueIndexedProperty)
                .GetProperty("AggregateType")
                .Should()
                .BeDecoratedWith<StringLengthAttribute>(a => a.MaximumLength == 128);
        }

        [TestMethod]
        public void sut_has_PropertyName_property()
        {
            typeof(UniqueIndexedProperty).Should().HaveProperty<string>("PropertyName");
        }

        [TestMethod]
        public void PropertyName_is_decorated_with_StringLength()
        {
            typeof(UniqueIndexedProperty)
                .GetProperty("PropertyName")
                .Should()
                .BeDecoratedWith<StringLengthAttribute>(a => a.MaximumLength == 128);
        }

        [TestMethod]
        public void sut_has_PropertyValue_property()
        {
            typeof(UniqueIndexedProperty).Should().HaveProperty<string>("PropertyValue");
        }

        [TestMethod]
        public void PropertyValue_is_decorated_with_StringLength()
        {
            typeof(UniqueIndexedProperty)
                .GetProperty("PropertyValue")
                .Should()
                .BeDecoratedWith<StringLengthAttribute>(a => a.MaximumLength == 128);
        }

        [TestMethod]
        public void sut_has_AggregateId_property()
        {
            typeof(UniqueIndexedProperty).Should().HaveProperty<Guid>("AggregateId");
        }

        [TestMethod]
        public void sut_has_Version_property()
        {
            typeof(UniqueIndexedProperty).Should().HaveProperty<int>("Version");
        }

        [TestMethod]
        public void Version_is_decorated_with_ConcurrencyCheck()
        {
            typeof(UniqueIndexedProperty)
                .GetProperty("Version")
                .Should()
                .BeDecoratedWith<ConcurrencyCheckAttribute>();
        }
    }
}
