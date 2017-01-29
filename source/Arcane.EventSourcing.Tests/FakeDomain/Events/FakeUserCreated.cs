using System.Collections.Generic;
using Newtonsoft.Json;
using Arcane.EventSourcing;
using Arcane.EventSourcing.Sql;

namespace Arcane.FakeDomain.Events
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
