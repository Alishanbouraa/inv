// ApplicationModeChangedEvent.cs
namespace QuickTechSystems.Application.Events
{
    public class ApplicationModeChangedEvent
    {
        public bool IsRestaurantMode { get; }

        public ApplicationModeChangedEvent(bool isRestaurantMode)
        {
            IsRestaurantMode = isRestaurantMode;
        }
    }
}