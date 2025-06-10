using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace QuickTechSystems.WPF.Views
{
    /// <summary>
    /// Converter to dynamically adjust column widths based on restaurant mode
    /// </summary>
    public class RestaurantModeColumnWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isRestaurantMode = (bool)value;
            string paramValue = parameter as string;

            // Parameter "right" is for the right column, default is for left column
            if (paramValue == "right")
            {
                return isRestaurantMode ? new GridLength(400) : new GridLength(0);
            }
            else // Left column
            {
                return new GridLength(1, GridUnitType.Star);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}