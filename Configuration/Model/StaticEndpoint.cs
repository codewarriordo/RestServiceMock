using Newtonsoft.Json.Linq;
using ServiceMock.Config;
namespace ServiceMockTemp.Configuration.Model
{
    public class StaticEndpoint
    {
        public ServiceMock.Config.Endpoint EndpointConfig { get; set; }

        public JObject ContentObject { get; set; }

        public List<DynamicProperty> DynamicPropertys { get; set; }

    }
}