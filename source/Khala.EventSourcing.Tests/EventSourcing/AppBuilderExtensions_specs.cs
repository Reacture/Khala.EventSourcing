namespace Khala.EventSourcing
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.Owin.BuilderProperties;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Owin;
    using Ploeh.AutoFixture;
    using Ploeh.AutoFixture.AutoMoq;
    using Ploeh.AutoFixture.Idioms;

    [TestClass]
    [Ignore("Khala.EventSourcing.Owin 프로젝트는 더이상 지원되지 않는다. 이후 ASP.NET Core를 위한 유사 프로젝트가 지원될 계획이다.")]
    public class AppBuilderExtensions_specs
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Justification = "Should be mockable.")]
        public class FakeEventSourced : IEventSourced
        {
            public IEnumerable<IDomainEvent> FlushPendingEvents() => throw new NotImplementedException();

            public Guid Id => throw new NotImplementedException();

            public int Version => throw new NotImplementedException();
        }

        [TestMethod]
        public void sut_has_guard_clauses()
        {
            IFixture builder = new Fixture().Customize(new AutoMoqCustomization());
            new GuardClauseAssertion(builder).Verify(typeof(AppBuilderExtensions));
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void EnqueuePendingEvents_invokes_EnqueueAll_with_OnAppDisposing_once(bool canceled)
        {
            // Arrange
            IAppBuilder app = Mock.Of<IAppBuilder>(
                x => x.Properties == new Dictionary<string, object>());

            IEventPublisher eventPublisher = Mock.Of<IEventPublisher>();

            var cancellationToken = new CancellationToken(canceled);
            new AppProperties(app.Properties) { OnAppDisposing = cancellationToken };

            // Act
            app.EnqueuePendingEvents(eventPublisher);

            // Assert
            Mock.Get(eventPublisher).Verify(x => x.EnqueueAll(cancellationToken), Times.Once());
        }
    }
}
