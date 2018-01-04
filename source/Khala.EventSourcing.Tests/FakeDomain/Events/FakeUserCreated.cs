namespace Khala.FakeDomain.Events
{
    using System.Collections.Generic;
    using Khala.EventSourcing;
    using Khala.EventSourcing.Sql;
    using Newtonsoft.Json;

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
                    ["Username"] = Username,
                };
            }
        }
    }
}
