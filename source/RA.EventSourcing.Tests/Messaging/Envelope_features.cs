using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ploeh.AutoFixture;
using Ploeh.AutoFixture.Idioms;

namespace ReactiveArchitecture.Messaging
{
    [TestClass]
    public class Envelope_features
    {
        [TestMethod]
        public void Envelope_has_guard_clauses()
        {
            var fixture = new Fixture();

            fixture.Register<
                Dictionary<string, object>,
                IReadOnlyDictionary<string, object>
                >(dict => dict);

            var assertion = new GuardClauseAssertion(fixture);

            assertion.Verify(typeof(Envelope));
        }
    }
}
