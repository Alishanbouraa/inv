using System;
using System.Globalization;
using System.Windows.Data;

namespace QuickTechSystems.WPF.Converters
{
    public class DecimalToBoolConverter : IValueConverter
    {
        public object TrueValue { get; set; } = 0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
            {
                if (parameter != null && parameter.ToString() == "Visibility")
                {
                    // For visibility conversion
                    return decimalValue == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                }

                // For boolean conversion
                return decimalValue == (decimal)TrueValue;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}