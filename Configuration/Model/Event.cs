using Newtonsoft.Json.Linq;
using ServiceMockTemp.Configuration.Model;

namespace ServiceMock.Config
{
    public class Event
    {
        public string FileName { get; set; }
        public int TimeOffset { get; set; }
        public bool Processed { get; set; }
        public string EventContent { get; set; }

        public JObject EventTemplate { get; set; }

        public List<DynamicProperty> DynamicPropertys { get; set; }
    }
}
