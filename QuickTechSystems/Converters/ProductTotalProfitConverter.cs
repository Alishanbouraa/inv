using QuickTechSystems.Application.DTOs;
using System;
using System.Globalization;
using System.Windows.Data;

namespace QuickTechSystems.WPF.Converters
{
    public class ProductTotalProfitConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is ProductDTO product && product != null)
                {
                    decimal totalSale = product.SalePrice * product.CurrentStock;
                    decimal totalCost = product.PurchasePrice * product.CurrentStock;
                    decimal profit = Math.Round(totalSale - totalCost, 2);

                    // If parameter is "Check", return the sign as a string
                    if (parameter != null && parameter.ToString() == "Check")
                    {
                        if (profit < 0) return "Negative";
                        if (profit > 0) return "Positive";
                        return "Zero";
                    }

                    return profit;
                }
                return 0;
            }
            catch (Exception)
            {
                // Return 0 if any calculation error occurs
                return parameter != null && parameter.ToString() == "Check" ? "Zero" : 0;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}