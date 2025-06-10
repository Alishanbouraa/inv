// Path: QuickTechSystems.WPF.Converters/NullToBoolConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace QuickTechSystems.WPF.Converters
{
    /// <summary>
    /// Converts null values to false and non-null values to true
    /// </summary>
    public class NullToBoolConverter : IValueConverter
    {
        private static NullToBoolConverter _instance;

        /// <summary>
        /// Singleton instance for static resource usage
        /// </summary>
        public static NullToBoolConverter Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new NullToBoolConverter();
                return _instance;
            }
        }

        /// <summary>
        /// Converts a value to boolean based on null check
        /// </summary>
        /// <param name="value">The value to convert</param>
        /// <param name="targetType">The target type (not used)</param>
        /// <param name="parameter">Optional parameter for inversion (set to "Invert" to invert the result)</param>
        /// <param name="culture">The culture (not used)</param>
        /// <returns>False if value is null, True if value is not null (or inverted if parameter is "Invert")</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool result = value != null;

            // Check if we should invert the result
            if (parameter != null && parameter.ToString().Equals("Invert", StringComparison.OrdinalIgnoreCase))
            {
                result = !result;
            }

            return result;
        }

        /// <summary>
        /// Not implemented - this converter is one-way only
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("NullToBoolConverter is a one-way converter only.");
        }
    }
}