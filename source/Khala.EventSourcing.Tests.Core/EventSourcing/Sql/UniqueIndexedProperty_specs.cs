namespace Khala.EventSourcing.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using FluentAssertions;
    using Xunit;

    public class UniqueIndexedProperty_specs
    {
        [Fact]
        public void IndexName_is_correct()
        {
            UniqueIndexedProperty.IndexName.Should().Be("SqlEventStore_IX_AggregateId_PropertyName");
        }

        [Fact]
        public void sut_has_AggregateType_property()
        {
            typeof(UniqueIndexedProperty).Should().HaveProperty<string>("AggregateType");
        }

        [Fact]
        public void AggregateType_is_decorated_with_Key()
        {
            typeof(UniqueIndexedProperty)
                .GetProperty("AggregateType")
                .Should()
                .BeDecoratedWith<KeyAttribute>();
        }

        [Fact]
        public void AggregateType_is_decorated_with_Column()
        {
            typeof(UniqueIndexedProperty)
                .GetProperty("AggregateType")
                .Should()
                .BeDecoratedWith<ColumnAttribute>(a => a.Order == 0);
        }

        [Fact]
        public void AggregateType_is_decorated_with_StringLength()
        {
            typeof(UniqueIndexedProperty)
                .GetProperty("AggregateType")
                .Should()
                .BeDecoratedWith<StringLengthAttribute>(a => a.MaximumLength == 128);
        }

        [Fact]
        public void sut_has_PropertyName_property()
        {
            typeof(UniqueIndexedProperty).Should().HaveProperty<string>("PropertyName");
        }

        [Fact]
        public void PropertyName_is_decorated_with_Key()
        {
            typeof(UniqueIndexedProperty)
                .GetProperty("PropertyName")
                .Should()
                .BeDecoratedWith<KeyAttribute>();
        }

        [Fact]
        public void PropertyName_is_decorated_with_Column()
        {
            typeof(UniqueIndexedProperty)
                .GetProperty("PropertyName")
                .Should()
                .BeDecoratedWith<ColumnAttribute>(a => a.Order == 1);
        }

        [Fact]
        public void PropertyName_is_decorated_with_StringLength()
        {
            typeof(UniqueIndexedProperty)
                .GetProperty("PropertyName")
                .Should()
                .BeDecoratedWith<StringLengthAttribute>(a => a.MaximumLength == 128);
        }

        [Fact]
        public void sut_has_PropertyValue_property()
        {
            typeof(UniqueIndexedProperty).Should().HaveProperty<string>("PropertyValue");
        }

        [Fact]
        public void PropertyValue_is_decorated_with_Key()
        {
            typeof(UniqueIndexedProperty)
                .GetProperty("PropertyValue")
                .Should()
                .BeDecoratedWith<KeyAttribute>();
        }

        [Fact]
        public void PropertyValue_is_decorated_with_Column()
        {
            typeof(UniqueIndexedProperty)
                .GetProperty("PropertyValue")
                .Should()
                .BeDecoratedWith<ColumnAttribute>(a => a.Order == 2);
        }

        [Fact]
        public void PropertyValue_is_decorated_with_StringLength()
        {
            typeof(UniqueIndexedProperty)
                .GetProperty("PropertyValue")
                .Should()
                .BeDecoratedWith<StringLengthAttribute>(a => a.MaximumLength == 128);
        }

        [Fact]
        public void sut_has_AggregateId_property()
        {
            typeof(UniqueIndexedProperty).Should().HaveProperty<Guid>("AggregateId");
        }

        [Fact]
        public void sut_has_Version_property()
        {
            typeof(UniqueIndexedProperty).Should().HaveProperty<int>("Version");
        }

        [Fact]
        public void Version_is_decorated_with_ConcurrencyCheck()
        {
            typeof(UniqueIndexedProperty)
                .GetProperty("Version")
                .Should()
                .BeDecoratedWith<ConcurrencyCheckAttribute>();
        }
    }
}
