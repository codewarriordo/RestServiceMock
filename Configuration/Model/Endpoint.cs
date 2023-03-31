using Newtonsoft.Json.Linq;
namespace ServiceMock.Config
{
    public class Endpoint
    {
        private string _path;
        public string Path {  get { return _path; }
        set
        {
            _path = value.ToLower();
        } }
        public RestMethod Method { get; set; }
        public string ResponseFile { get; set; }

        public string ResponseContent { get; set; }

        public int ReturnCode { get; set; }

        public bool LongPoll { get; set; }

   



    }
}
