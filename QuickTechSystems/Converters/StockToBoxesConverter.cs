// Path: QuickTechSystems.WPF.Converters/StockToBoxesConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace QuickTechSystems.WPF.Converters
{
    public class StockToBoxesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal stock)
            {
                // Get the item per box directly from the source object
                int itemsPerBox = 0;

                // Extract from explicit parameter if provided
                if (parameter is int directInt)
                {
                    itemsPerBox = directInt;
                }
                else if (parameter is string itemsStr && int.TryParse(itemsStr, out int parsedItems))
                {
                    itemsPerBox = parsedItems;
                }

                // Calculate number of boxes if items per box > 0
                if (itemsPerBox > 0)
                {
                    return Math.Floor(stock / itemsPerBox).ToString("N0");
                }
            }

            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}