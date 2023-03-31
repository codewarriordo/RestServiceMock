
using ServiceMock.Configuration.Model;

namespace ServiceMock.Config
{
    public enum  RestMethod  {GET, POST,DELETE};
    public class BaseConfig
    {
        public List<Endpoint> Endpoints { get; set; }

         public List<WebHook> WebHooks { get; set; }

        public List<LoopIdentifier> LoopIdentifier { get; set; }
    
    
              
    }
}
