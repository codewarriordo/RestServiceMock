
namespace ServiceMock.Config
{
    public class EventSequence
    {
        public String Name { get; set; }
        public int LoopCount { get; set; }
        public int CurrentLoop { get; set; }
        public int LastOffset { get; set; }
        public List<Event> Events { get; set; }
    


    }
}
