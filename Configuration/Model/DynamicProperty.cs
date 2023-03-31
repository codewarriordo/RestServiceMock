using Newtonsoft.Json.Linq;
using ServiceMock.Config;

namespace ServiceMockTemp.Configuration.Model
{
    public enum  DynamicType  {INT, TIMESTAMP,UUID, ENUMERATION, LOOP_IDENTIFIER};
    public class DynamicProperty
    {
        public DynamicType Type { get; set; }
        public JProperty JsonProperty { get; set; }

        public string[] PossibleEnumValues { get; set; }

        public LoopIdentifier LoopIdent { get; set; }

    }
    
}