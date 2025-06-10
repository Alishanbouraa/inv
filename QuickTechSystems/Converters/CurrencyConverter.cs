using System;
using System.Globalization;
using System.Windows.Data;
using QuickTechSystems.Application.Helpers;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace QuickTechSystems.WPF.Converters
{
    public class CurrencyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal amount)
            {
                // If parameter is "LBP", convert USD to LBP
                if (parameter is string currencyCode && currencyCode == "LBP")
                {
                    // Convert USD to LBP using the helper
                    decimal lbpAmount = CurrencyHelper.ConvertToLBP(amount);
                    return CurrencyHelper.FormatLBP(lbpAmount);
                }
                // Default to USD
                return string.Format("{0:C2}", amount);
            }
            return "0.00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is string stringValue)
                {
                    // Remove currency symbols, commas, and trim whitespace
                    string cleanedValue = stringValue
                        .Replace("$", "")
                        .Replace("LBP", "")
                        .Replace(",", "")
                        .Trim();

                    // Handle case with multiple decimal points - keep only first one
                    int firstDecimalIndex = cleanedValue.IndexOf('.');
                    if (firstDecimalIndex >= 0)
                    {
                        string beforeDecimal = cleanedValue.Substring(0, firstDecimalIndex + 1);
                        string afterDecimal = cleanedValue.Substring(firstDecimalIndex + 1);

                        // Remove any additional decimal points from the after-decimal part
                        afterDecimal = afterDecimal.Replace(".", "");

                        cleanedValue = beforeDecimal + afterDecimal;
                    }

                    // Try to parse the cleaned value
                    if (decimal.TryParse(cleanedValue, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
                    {
                        // If parameter is "LBP", we need to convert back from LBP to USD
                        if (parameter is string currencyCode && currencyCode == "LBP")
                        {
                            // Convert from LBP back to USD if needed
                            // For example: result = result / CurrencyHelper.GetExchangeRate();
                            // For now, we'll just return the parsed value
                        }

                        return result;
                    }

                    // Additional fallback: try to extract just the numeric parts using regex
                    var regex = new Regex(@"\d+\.?\d*");
                    var match = regex.Match(stringValue);
                    if (match.Success && decimal.TryParse(match.Value, out decimal regexResult))
                    {
                        return regexResult;
                    }

                    // Log the error for debugging
                    Debug.WriteLine($"CurrencyConverter failed to convert: '{stringValue}'");
                }

                // Return 0 if no conversion was possible
                return 0.00m;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in CurrencyConverter.ConvertBack: {ex.Message}");
                return 0.00m;
            }
        }
    }
}