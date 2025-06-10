using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace QuickTechSystems.WPF.Converters
{
    /// <summary>
    /// Converts a boolean value to a GridLength for dynamic column sizing
    /// </summary>
    public class BooleanTo60PercentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isEnabled = (bool)value;

            if (isEnabled)
                return new GridLength(60, GridUnitType.Star);
            else
                return new GridLength(1, GridUnitType.Star); // Full width if not in restaurant mode
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BooleanTo40PercentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isEnabled = (bool)value;

            if (isEnabled)
                return new GridLength(40, GridUnitType.Star);
            else
                return new GridLength(0); // Collapse if not in restaurant mode
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}