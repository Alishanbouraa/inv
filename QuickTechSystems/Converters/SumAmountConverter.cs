// Path: QuickTechSystems.WPF.Converters/SumAmountConverter.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using QuickTechSystems.Application.DTOs;

namespace QuickTechSystems.WPF.Converters
{
    public class SumAmountConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable<SupplierTransactionDTO> payments)
            {
                decimal sum = payments.Sum(p => Math.Abs(p.Amount));
                return string.Format(culture, "{0:C}", sum);
            }

            return "$0.00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}