using ServiceMock.Config;

namespace ServiceMock.Configuration.Model
{
    public class WebHook
    {
        public string TargetUrl { get; set; }

        public string EventSequence { get; set; }


        public EventConfig EventConfig { get; set; }
    }
}