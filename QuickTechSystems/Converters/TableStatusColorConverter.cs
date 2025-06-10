using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace QuickTechSystems.WPF.Converters
{
    public class TableStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status.ToLower() switch
                {
                    "available" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),   // Green
                    "occupied" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),    // Red
                    "reserved" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),   // Amber
                    "maintenance" => new SolidColorBrush(Color.FromRgb(107, 114, 128)), // Gray
                    _ => new SolidColorBrush(Color.FromRgb(209, 213, 219))            // Light Gray
                };
            }

            return new SolidColorBrush(Color.FromRgb(209, 213, 219)); // Default Light Gray
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}