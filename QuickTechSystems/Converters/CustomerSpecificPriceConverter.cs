using System;
using System.Globalization;
using System.Windows.Data;

namespace QuickTechSystems.WPF.Converters
{
    public class CustomerSpecificPriceConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 3 || values[0] == null || values[1] == null)
                return "N/A";

            if (values[0] is decimal productPrice && values[1] is int productId)
            {
                var dictionary = values[2] as Dictionary<int, decimal>;
                if (dictionary != null && dictionary.TryGetValue(productId, out decimal customPrice))
                {
                    return customPrice.ToString("C2", culture);
                }
                return productPrice.ToString("C2", culture);
            }

            return "N/A";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}