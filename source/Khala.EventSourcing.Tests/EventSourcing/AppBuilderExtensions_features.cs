namespace Khala.EventSourcing
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.Owin.BuilderProperties;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Owin;

    [TestClass]
    public class AppBuilderExtensions_features
    {
        public class FooAppBuilder : IAppBuilder
        {
            public IDictionary<string, object> Properties => throw new NotImplementedException();

            public object Build(Type returnType)
            {
                throw new NotImplementedException();
            }

            public IAppBuilder New()
            {
                throw new NotImplementedException();
            }

            public IAppBuilder Use(object middleware, params object[] args)
            {
                throw new NotImplementedException();
            }
        }

        public class FooRepository : IEventSourcedRepository<IEventSourced>
        {
            public IEventPublisher EventPublisher => throw new NotImplementedException();

            public Task<IEventSourced> Find(Guid sourceId, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task Save(IEventSourced source, Guid? correlationId, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }

        public class FooEventSourced : IEventSourced
        {
            public IEnumerable<IDomainEvent> PendingEvents => throw new NotImplementedException();

            public Guid Id => throw new NotImplementedException();

            public int Version => throw new NotImplementedException();
        }

        [TestMethod]
        public void EnqueuePendingEvents_has_guard_clause_against_null_app_argument()
        {
            // Arrange
            IAppBuilder app = null;
            IEventSourcedRepository<IEventSourced> repository = new FooRepository();

            // Act
            object exception = null;
            try
            {
                AppBuilderExtensions.EnqueuePendingEvents(app, repository);
            }
            catch (Exception thrown)
            {
                exception = thrown;
            }

            // Assert
            exception.Should().NotBeNull()
                .And.BeOfType<ArgumentNullException>()
                .Which.ParamName.Should().Be("app");
        }

        [TestMethod]
        public void EnqueuePendingEvents_has_guard_clause_against_null_repository_argument()
        {
            // Arrange
            IAppBuilder app = new FooAppBuilder();
            IEventSourcedRepository<IEventSourced> repository = null;

            // Act
            Action action = () =>
            AppBuilderExtensions.EnqueuePendingEvents(app, repository);

            // Assert
            action.ShouldThrow<ArgumentNullException>().Where(x => x.ParamName == "repository");
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
            IEventSourcedRepository<FooEventSourced> repository =
                Mock.Of<IEventSourcedRepository<FooEventSourced>>(
                    x => x.EventPublisher == eventPublisher);

            var cancellationToken = new CancellationToken(canceled);
            var appProperties = new AppProperties(app.Properties);
            appProperties.OnAppDisposing = cancellationToken;

            // Act
            app.EnqueuePendingEvents(repository);

            // Assert
            Mock.Get(eventPublisher)
                .Verify(x => x.EnqueueAll(cancellationToken), Times.Once());
        }
    }
}
