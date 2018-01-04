namespace Khala.FakeDomain.Events
{
    using System;
    using System.Collections.Generic;
    using Khala.EventSourcing.Sql;
    using Newtonsoft.Json;

    public class FakeUsernameChanged : FakeDomainEvent, IUniqueIndexedDomainEvent
    {
        public string Username { get; set; } = Guid.NewGuid().ToString();

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
