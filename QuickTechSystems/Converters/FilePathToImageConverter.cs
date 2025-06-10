// Path: QuickTechSystems.WPF.Converters/FilePathToImageConverter.cs
using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace QuickTechSystems.WPF.Converters
{
    public class FilePathToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                string imagePath = value as string;

                if (string.IsNullOrWhiteSpace(imagePath))
                    return null;

                // Check if file exists
                if (!File.Exists(imagePath))
                    return null;

                // Create BitmapImage
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

                // If parameter is provided, use it as decode pixel width for optimization
                if (parameter != null && int.TryParse(parameter.ToString(), out int decodeWidth))
                {
                    bitmap.DecodePixelWidth = decodeWidth;
                }

                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze(); // Important for performance and cross-thread access

                return bitmap;
            }
            catch (Exception)
            {
                // Return null if image cannot be loaded
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}