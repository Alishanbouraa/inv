// Path: QuickTechSystems.WPF.Converters/StringToVisibilityConverter.cs
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace QuickTechSystems.WPF.Converters
{
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                string stringValue = value as string;
                string parameterValue = parameter as string;

                bool hasValue = !string.IsNullOrWhiteSpace(stringValue);

                // Check if parameter is "Inverse" to invert the logic
                bool isInverse = string.Equals(parameterValue, "Inverse", StringComparison.OrdinalIgnoreCase);

                if (isInverse)
                {
                    return hasValue ? Visibility.Collapsed : Visibility.Visible;
                }
                else
                {
                    return hasValue ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch
            {
                return Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}