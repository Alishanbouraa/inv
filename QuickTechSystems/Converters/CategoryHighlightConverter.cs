using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace QuickTechSystems.WPF.Converters
{
    public class CategoryHighlightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var selectedCategory = value as CategoryDTO;
            var currentCategory = parameter as CategoryDTO;

            if (selectedCategory != null && currentCategory != null &&
                selectedCategory.CategoryId == currentCategory.CategoryId)
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3")); // Selected color
            }

            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0")); // Default color
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}