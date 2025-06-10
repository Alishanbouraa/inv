using System;
using System.Globalization;
using System.Windows.Data;

namespace QuickTechSystems.WPF.Converters
{
    public class DecimalConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
            {
                return decimalValue.ToString("N2", culture);
            }
            return "0.00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                // Handle empty or whitespace
                if (string.IsNullOrWhiteSpace(stringValue))
                    return 0m;

                // Try parsing with the current culture
                if (decimal.TryParse(stringValue, NumberStyles.Any, culture, out decimal result))
                    return result;

                // Try parsing with invariant culture as fallback
                if (decimal.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
                    return result;
            }
            return 0m;
        }
    }
}