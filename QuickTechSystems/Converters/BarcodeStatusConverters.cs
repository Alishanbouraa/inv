// Path: QuickTechSystems.WPF.Converters/BarcodeStatusConverters.cs
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace QuickTechSystems.WPF.Converters
{
    /// <summary>
    /// Converts byte array (barcode image) to color for status indicator
    /// </summary>
    public class BarcodeStatusToColorConverter : IValueConverter
    {
        public static readonly BarcodeStatusToColorConverter Instance = new BarcodeStatusToColorConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Handle null DataContext case
            if (value == null)
                return Colors.Gray; // Default color when no data is available

            bool hasBarcode = false;

            if (value is byte[] byteArray)
            {
                hasBarcode = byteArray != null && byteArray.Length > 0;
            }

            // Return Green for ready, Red for missing
            return hasBarcode ? Colors.Green : Colors.Red;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts byte array (barcode image) to text for status indicator
    /// </summary>
    public class BarcodeStatusToTextConverter : IValueConverter
    {
        public static readonly BarcodeStatusToTextConverter Instance = new BarcodeStatusToTextConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Handle null DataContext case
            if (value == null)
                return "N/A"; // Default text when no data is available

            bool hasBarcode = false;

            if (value is byte[] byteArray)
            {
                hasBarcode = byteArray != null && byteArray.Length > 0;
            }

            // Return "Ready" for ready, "Missing" for missing
            return hasBarcode ? "Ready" : "Missing";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}