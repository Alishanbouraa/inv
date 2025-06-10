using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace QuickTechSystems.WPF.Converters
{
    public class EmptyStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                bool isEmpty = string.IsNullOrWhiteSpace(stringValue);

                // Check if parameter requests inverse logic
                bool inverse = parameter?.ToString()?.ToLower() == "inverse";

                if (inverse)
                {
                    return isEmpty ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    return isEmpty ? Visibility.Collapsed : Visibility.Visible;
                }
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}