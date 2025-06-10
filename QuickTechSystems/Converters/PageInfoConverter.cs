// QuickTechSystems.WPF.Converters/PageInfoConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace QuickTechSystems.WPF.Converters
{
    public class PageInfoConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is int currentPage && values[1] is int totalPages)
            {
                string totalCount = values.Length >= 3 && values[2] is int total ? total.ToString() : "0";
                return $"Page {currentPage} of {totalPages} ({totalCount} total)";
            }

            return "Page 0 of 0";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}