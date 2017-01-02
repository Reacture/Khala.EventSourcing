using System.Collections.Generic;
using ReactiveArchitecture.EventSourcing;
using ReactiveArchitecture.EventSourcing.Sql;

namespace ReactiveArchitecture.FakeDomain.Events
{
    public class FakeUsernameChanged : DomainEvent, IUniqueIndexedDomainEvent
    {
        public string Username { get; set; }

        public IReadOnlyDictionary<string, string> UniqueIndexedProperties
        {
            get
            {
                return new Dictionary<string, string>
                {
                    ["Username"] = Username
                };
            }
        }
    }
}
