using System;
using System.Globalization;
using System.Windows.Data;

namespace QuickTechSystems.WPF.Converters
{
    /// <summary>
    /// Converter that compares two decimal values and returns whether they are different.
    /// Used for highlighting custom prices that differ from default prices.
    /// </summary>
    public class PriceComparisonConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] == null || values[1] == null)
                return false;

            try
            {
                decimal customPrice = System.Convert.ToDecimal(values[0]);
                decimal defaultPrice = System.Convert.ToDecimal(values[1]);

                // Return true if prices are different
                const decimal epsilon = 0.00001m;
                return Math.Abs(customPrice - defaultPrice) > epsilon;
            }
            catch
            {
                return false;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack is not supported in PriceComparisonConverter");
        }
    }
}