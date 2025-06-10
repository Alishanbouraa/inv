using System.Windows;
using System.Windows.Media;

namespace QuickTechSystems.WPF.Extensions
{
    public static class UIElementExtensions
    {
        public static T FindParentOfType<T>(this DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentDepObj = VisualTreeHelper.GetParent(child);

            if (parentDepObj == null) return null;

            T parent = parentDepObj as T;
            if (parent != null)
            {
                return parent;
            }
            else
            {
                return FindParentOfType<T>(parentDepObj);
            }
        }
    }
}