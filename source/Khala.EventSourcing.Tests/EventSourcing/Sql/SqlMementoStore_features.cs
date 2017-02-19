using System;
using Khala.Messaging;
using Ploeh.AutoFixture;
using Ploeh.AutoFixture.AutoMoq;
using Ploeh.AutoFixture.Idioms;
using Xunit;
using Xunit.Abstractions;

namespace Khala.EventSourcing.Sql
{
    public class SqlMementoStore_features
    {
        private ITestOutputHelper output;
        private IFixture fixture;
        private IMessageSerializer serializer;

        public class DataContext : MementoStoreDbContext
        {
        }

        public SqlMementoStore_features(ITestOutputHelper output)
        {
            this.output = output;

            fixture = new Fixture().Customize(new AutoMoqCustomization());
            fixture.Inject<Func<IMementoStoreDbContext>>(() => new DataContext());

            serializer = new JsonMessageSerializer();
            fixture.Inject(serializer);

            using (var db = new DataContext())
            {
                db.Database.Log = output.WriteLine;
                db.Database.ExecuteSqlCommand("DELETE FROM Mementoes");
            }
        }

        [Fact]
        public void SqlMementoStore_has_guard_clauses()
        {
            var assertion = new GuardClauseAssertion(fixture);
            assertion.Verify(typeof(SqlMementoStore));
        }
    }
}
