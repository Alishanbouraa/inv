using System;
using System.Globalization;
using System.Windows.Data;

namespace QuickTechSystems.WPF.Converters
{
    /// <summary>
    /// Converter that determines if two values are not equal to each other.
    /// Used for highlighting custom prices that differ from default prices.
    /// </summary>
    public class NotEqualValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Handle null cases
            if (value == null && parameter == null)
                return false; // Both null means they are equal

            if (value == null || parameter == null)
                return true; // One null, one not null means they're not equal

            // Handle numeric comparisons with tolerance for floating point
            if (IsNumeric(value) && IsNumeric(parameter))
            {
                // Convert both to decimal for comparison
                decimal val1 = System.Convert.ToDecimal(value);
                decimal val2 = System.Convert.ToDecimal(parameter);

                // Use a small epsilon for decimal comparison to handle floating point issues
                const decimal epsilon = 0.00001m;
                return Math.Abs(val1 - val2) > epsilon;
            }

            // For other types, use regular equality
            return !value.Equals(parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack is not supported in NotEqualValueConverter");
        }

        /// <summary>
        /// Checks if an object is a numeric type
        /// </summary>
        private bool IsNumeric(object value)
        {
            return value is sbyte || value is byte ||
                   value is short || value is ushort ||
                   value is int || value is uint ||
                   value is long || value is ulong ||
                   value is float || value is double ||
                   value is decimal;
        }
    }
}