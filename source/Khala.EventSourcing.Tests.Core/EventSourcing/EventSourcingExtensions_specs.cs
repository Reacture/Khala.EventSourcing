namespace Khala.EventSourcing
{
    using System.Threading;
    using AutoFixture;
    using AutoFixture.AutoMoq;
    using AutoFixture.Idioms;
    using AutoFixture.Kernel;
    using Khala.FakeDomain;
    using Khala.Messaging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class EventSourcingExtensions_specs
    {
        [TestMethod]
        public void sut_has_guard_clauses()
        {
            IFixture builder = new Fixture().Customize(new AutoMoqCustomization());
            new GuardClauseAssertion(builder).Verify(typeof(EventSourcingExtensions));
        }

        [TestMethod]
        public void SaveAndPublish_with_Envelope_relays_correctly()
        {
            // Arrange
            var fixture = new Fixture();
            var factory = new MethodInvoker(new GreedyConstructorQuery());
            fixture.Customize<Envelope>(c => c.FromFactory(factory));
            IEnvelope correlation = fixture.Create<Envelope>();
            FakeUser source = fixture.Create<FakeUser>();
            CancellationToken cancellationToken = new CancellationTokenSource().Token;
            IEventSourcedRepository<FakeUser> repository = Mock.Of<IEventSourcedRepository<FakeUser>>();

            // Act
            repository.SaveAndPublish(source, correlation, cancellationToken);

            // Assert
            Mock.Get(repository).Verify(
                x =>
                x.SaveAndPublish(
                    source,
                    correlation.OperationId,
                    correlation.MessageId,
                    correlation.Contributor,
                    cancellationToken),
                Times.Once());
        }
    }
}
