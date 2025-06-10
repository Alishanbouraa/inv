using System;
using System.Globalization;
using System.Windows.Data;

namespace QuickTechSystems.WPF.Converters
{
    public class MultiValueStockToBoxesConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] != null && values[1] != null)
            {
                if (int.TryParse(values[0].ToString(), out int currentStock) &&
                    int.TryParse(values[1].ToString(), out int itemsPerBox) &&
                    itemsPerBox > 0)
                {
                    int completeBoxes = currentStock / itemsPerBox;
                    return completeBoxes.ToString("N0");
                }
            }
            return "0";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}