using System;
using System.Globalization;
using System.Windows.Data;

namespace QuickTechSystems.WPF.Converters
{
    public class NegativeValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
            {
                return decimalValue < 0
                    ? $"-{culture.NumberFormat.CurrencySymbol}{Math.Abs(decimalValue).ToString("N2", culture)}"
                    : $"{culture.NumberFormat.CurrencySymbol}{decimalValue.ToString("N2", culture)}";
            }

            if (value is double doubleValue)
            {
                return doubleValue < 0
                    ? $"-{culture.NumberFormat.CurrencySymbol}{Math.Abs(doubleValue).ToString("N2", culture)}"
                    : $"{culture.NumberFormat.CurrencySymbol}{doubleValue.ToString("N2", culture)}";
            }

            if (value is float floatValue)
            {
                return floatValue < 0
                    ? $"-{culture.NumberFormat.CurrencySymbol}{Math.Abs(floatValue).ToString("N2", culture)}"
                    : $"{culture.NumberFormat.CurrencySymbol}{floatValue.ToString("N2", culture)}";
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                stringValue = stringValue.Replace(culture.NumberFormat.CurrencySymbol, "")
                                      .Replace(" ", "");

                if (decimal.TryParse(stringValue, NumberStyles.Currency, culture, out decimal result))
                {
                    return result;
                }
            }
            return value;
        }
    }
}