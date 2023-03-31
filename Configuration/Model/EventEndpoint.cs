using Newtonsoft.Json.Linq;

namespace ServiceMock.Config
{
    public class EventEndpoint
    {
        public Endpoint EndpointConfig { get; set; }

        public EventConfig EventConfig { get; set; }

        public JObject ResponseDocument { get; set; }

        public JArray EventArray { get; set; }


    }
}