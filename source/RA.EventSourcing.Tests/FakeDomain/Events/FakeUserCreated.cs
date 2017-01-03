using System.Collections.Generic;
using Newtonsoft.Json;
using ReactiveArchitecture.EventSourcing;
using ReactiveArchitecture.EventSourcing.Sql;

namespace ReactiveArchitecture.FakeDomain.Events
{
    public class FakeUserCreated : DomainEvent, IUniqueIndexedDomainEvent
    {
        public string Username { get; set; }

        [JsonIgnore]
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
