namespace ReactiveArchitecture.Messaging
{
    using System;
    using System.IO;
    using Newtonsoft.Json;

    public class JsonMessageSerializer
    {
        private readonly JsonSerializer _serializer;

        public JsonMessageSerializer()
        {
            _serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects,
#if DEBUG
                Formatting = Formatting.Indented
#else
                Formatting = Formatting.None
#endif
            });
        }

        public string Serialize(object message)
        {
            using (var writer = new StringWriter())
            {
                _serializer.Serialize(writer, message);
                return writer.ToString();
            }
        }

        public object Deserialize(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            using (var reader = new StringReader(json))
            using (var jsonReader = new JsonTextReader(reader))
            {
                return _serializer.Deserialize(jsonReader);
            }
        }
    }
}
