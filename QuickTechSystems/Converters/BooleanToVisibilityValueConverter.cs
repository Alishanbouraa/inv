using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace QuickTechSystems.WPF.Converters
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BooleanToVisibilityValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = false;
            if (value is bool)
            {
                boolValue = (bool)value;
            }
            else if (value is bool?)
            {
                bool? nullable = (bool?)value;
                boolValue = nullable.GetValueOrDefault();
            }

            if (parameter != null && parameter.ToString().ToLower() == "inverse")
            {
                boolValue = !boolValue;
            }

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility)
            {
                bool result = (Visibility)value == Visibility.Visible;

                if (parameter != null && parameter.ToString().ToLower() == "inverse")
                {
                    result = !result;
                }

                return result;
            }
            return false;
        }
    }
}