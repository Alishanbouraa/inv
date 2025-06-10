// Path: QuickTechSystems.WPF.Converters/IsResolvedConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace QuickTechSystems.WPF.Converters
{
    public class IsResolvedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isResolved)
            {
                if (parameter?.ToString() == "background")
                {
                    return isResolved
                        ? new SolidColorBrush(Color.FromRgb(240, 253, 244)) // Light green for resolved
                        : new SolidColorBrush(Color.FromRgb(255, 241, 242)); // Light red for unresolved
                }
                else if (parameter?.ToString() == "text")
                {
                    return isResolved
                        ? new SolidColorBrush(Color.FromRgb(22, 163, 74))  // Green text
                        : new SolidColorBrush(Color.FromRgb(220, 38, 38)); // Red text
                }
                else if (parameter?.ToString() == "status")
                {
                    return isResolved ? "Resolved" : "Pending";
                }
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}