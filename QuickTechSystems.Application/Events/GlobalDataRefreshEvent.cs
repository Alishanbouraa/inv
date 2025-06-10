// Path: QuickTechSystems.Application.Events/GlobalDataRefreshEvent.cs

namespace QuickTechSystems.Application.Events
{
    public class GlobalDataRefreshEvent
    {
        public DateTime Timestamp { get; }

        public GlobalDataRefreshEvent()
        {
            Timestamp = DateTime.Now;
        }
    }
}