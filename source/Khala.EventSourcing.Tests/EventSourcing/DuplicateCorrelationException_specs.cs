namespace Khala.EventSourcing
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Ploeh.AutoFixture;
    using Ploeh.AutoFixture.Idioms;

    [TestClass]
    public class DuplicateCorrelationException_specs
    {
        [TestMethod]
        public void constructor_has_guard_clauses()
        {
            new GuardClauseAssertion(new Fixture()).Verify(typeof(DuplicateCorrelationException).GetConstructors());
        }
    }
}
