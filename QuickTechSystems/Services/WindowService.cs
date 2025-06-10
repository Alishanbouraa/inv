using System.Linq;
using System.Windows;

namespace QuickTechSystems.WPF.Services
{
    public class WindowService : IWindowService
    {
        public Window GetCurrentWindow()
        {
            return System.Windows.Application.Current.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive);
        }
    }
}