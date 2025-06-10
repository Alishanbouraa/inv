using QuickTechSystems.Application.DTOs;
using System;
using System.Globalization;
using System.Windows.Data;

namespace QuickTechSystems.WPF.Converters
{
    public class ProductTotalValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is ProductDTO product && product != null)
                {
                    return product.SalePrice * product.CurrentStock;
                }
                return 0;
            }
            catch (Exception)
            {
                // Return 0 if any calculation error occurs
                return 0;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}