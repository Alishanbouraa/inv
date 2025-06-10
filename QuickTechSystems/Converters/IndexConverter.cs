// Path: QuickTechSystems.WPF.Converters/IndexConverter.cs
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace QuickTechSystems.WPF.Converters
{
    public class IndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ContentPresenter contentPresenter)
            {
                // Find the parent ItemsControl
                var itemsControl = FindParent<ItemsControl>(contentPresenter);
                if (itemsControl == null) return 0;

                // Find the index of the item in the ItemsControl
                var container = itemsControl.ItemContainerGenerator.ContainerFromItem(contentPresenter.DataContext);
                if (container == null) return 0;

                return itemsControl.ItemContainerGenerator.IndexFromContainer(container) + 1; // 1-based index
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null) return null;

            if (parentObject is T parent)
            {
                return parent;
            }
            else
            {
                return FindParent<T>(parentObject);
            }
        }
    }
}