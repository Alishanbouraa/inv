using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace QuickTechSystems.WPF.Converters
{
    /// <summary>
    /// Converts a transaction type string to a color for visual differentiation.
    /// </summary>
    public class TransactionTypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string transactionType)
            {
                switch (transactionType.ToLower())
                {
                    case "payment":
                        return new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green for payments

                    case "invoice":
                    case "purchase":
                        return new SolidColorBrush(Color.FromRgb(63, 81, 181)); // Indigo for invoices/purchases

                    case "refund":
                        return new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Orange for refunds

                    case "adjustment":
                        return new SolidColorBrush(Color.FromRgb(156, 39, 176)); // Purple for adjustments

                    default:
                        return new SolidColorBrush(Color.FromRgb(97, 97, 97)); // Gray for unknown types
                }
            }

            return new SolidColorBrush(Color.FromRgb(97, 97, 97)); // Default gray
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}