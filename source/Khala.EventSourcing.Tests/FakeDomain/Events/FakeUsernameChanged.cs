using System.Collections.Generic;
using Khala.EventSourcing;
using Khala.EventSourcing.Sql;
using Newtonsoft.Json;

namespace Khala.FakeDomain.Events
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
