// Path: QuickTechSystems.WPF.Converters/IsBoxCountInconsistentConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace QuickTechSystems.WPF.Converters
{
    public class IsBoxCountInconsistentConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 3 || values[0] == null || values[1] == null || values[2] == null)
                return false;

            if (!int.TryParse(values[0].ToString(), out int numberOfBoxes))
                return false;

            if (!decimal.TryParse(values[1].ToString(), out decimal currentStock))
                return false;

            if (!int.TryParse(values[2].ToString(), out int itemsPerBox) || itemsPerBox <= 0)
                return false;

            // Calculate expected number of boxes based on current stock
            int expectedBoxes = (int)Math.Floor(currentStock / itemsPerBox);

            // If box count and calculated box count differ by more than 1, flag as inconsistent
            return Math.Abs(numberOfBoxes - expectedBoxes) > 1;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}