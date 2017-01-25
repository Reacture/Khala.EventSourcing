using System.Threading;
using Moq;
using Ploeh.AutoFixture;
using Ploeh.AutoFixture.AutoMoq;
using Ploeh.AutoFixture.Idioms;
using ReactiveArchitecture.FakeDomain;
using Xunit;

namespace ReactiveArchitecture.EventSourcing
{
    public class EventSourcingExtensions_features
    {
        [Fact]
        public void class_has_guard_clauses()
        {
            var fixture = new Fixture().Customize(new AutoMoqCustomization());
            var assertion = new GuardClauseAssertion(fixture);
            assertion.Verify(typeof(EventSourcingExtensions));
        }

        [Fact]
        public void Save_relays_to_repository_with_null_correlation()
        {
            var fixture = new Fixture();
            var repository = Mock.Of<IEventSourcedRepository<FakeUser>>();
            var source = fixture.Create<FakeUser>();

            repository.Save(source, CancellationToken.None);

            Mock.Get(repository).Verify(
                x =>
                x.Save(source, null, CancellationToken.None),
                Times.Once());
        }
    }
}
