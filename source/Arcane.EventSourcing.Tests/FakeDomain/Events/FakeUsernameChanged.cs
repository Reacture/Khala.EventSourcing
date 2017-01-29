using System.Collections.Generic;
using Arcane.EventSourcing;
using Arcane.EventSourcing.Sql;
using Newtonsoft.Json;

namespace Arcane.FakeDomain.Events
{
    public class FakeUsernameChanged : DomainEvent, IUniqueIndexedDomainEvent
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
