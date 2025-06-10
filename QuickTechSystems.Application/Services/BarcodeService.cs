// Path: QuickTechSystems.Application.Services/BarcodeService.cs
using ZXing;
using ZXing.Common;
using ZXing.Windows.Compatibility;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using QuickTechSystems.Application.Services.Interfaces;
using System.Drawing;
using System.Drawing.Drawing2D;
using ZXing.QrCode.Internal;

namespace QuickTechSystems.Application.Services
{
    [SupportedOSPlatform("windows")]
    public class BarcodeService : IBarcodeService
    {
        public byte[] GenerateBarcode(string content, int width = 300, int height = 100)
        {
            try
            {
                // Clean the barcode but don't pad it
                string cleanedContent = CleanBarcode(content);

                var encodingOptions = new EncodingOptions
                {
                    Width = width * 3,
                    Height = height * 3,
                    Margin = 10,
                    PureBarcode = true
                };

                encodingOptions.Hints[EncodeHintType.ERROR_CORRECTION] = ErrorCorrectionLevel.H;
                encodingOptions.Hints[EncodeHintType.DISABLE_ECI] = true;

                var writer = new BarcodeWriter
                {
                    Format = BarcodeFormat.CODE_128, // Good for variable lengths
                    Options = encodingOptions
                };

                using var highResBitmap = writer.Write(cleanedContent);
                using var finalBitmap = CreateFinalBarcodeImage(highResBitmap, cleanedContent, width, height);

                using var stream = new MemoryStream();
                finalBitmap.Save(stream, ImageFormat.Png);

                return stream.ToArray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in barcode generation: {ex.Message}");
                return GenerateSimpleBarcode(content, width, height);
            }
        }

        /// <summary>
        /// Cleans the barcode by removing non-alphanumeric characters but preserves the original length and value
        /// </summary>
        private string CleanBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                return "000000000000"; // Default only for completely empty barcodes

            // Remove only whitespace and special characters, but keep alphanumeric
            // This allows for both numeric and alphanumeric barcodes
            string cleaned = new string(barcode.Where(c => char.IsLetterOrDigit(c)).ToArray());

            // If after cleaning we have nothing, return a default
            if (string.IsNullOrEmpty(cleaned))
                return "000000000000";

            // Return the cleaned barcode as-is, without any padding or truncation
            // The barcode library (CODE_128) can handle variable lengths from 1 to many characters
            return cleaned;
        }

        private Bitmap CreateFinalBarcodeImage(Bitmap highResBitmap, string text, int targetWidth, int targetHeight)
        {
            // Add space for text
            int textHeight = 25;
            int totalHeight = targetHeight + textHeight;

            // Create new bitmap at target size
            var finalBitmap = new Bitmap(targetWidth, totalHeight, PixelFormat.Format32bppArgb);

            using (var graphics = Graphics.FromImage(finalBitmap))
            {
                // Critical settings to prevent anti-aliasing and ensure solid bars
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.CompositingQuality = CompositingQuality.HighSpeed;
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor; // Critical for sharp bars
                graphics.SmoothingMode = SmoothingMode.None; // No smoothing for barcodes!
                graphics.PixelOffsetMode = PixelOffsetMode.Half;

                // White background
                graphics.Clear(Color.White);

                // Draw the high-res barcode scaled down to target size
                // This downsampling with NearestNeighbor keeps bars sharp
                graphics.DrawImage(highResBitmap, 0, 0, targetWidth, targetHeight);

                // Add text below barcode
                using (var font = new Font("Consolas", 10, FontStyle.Regular))
                using (var brush = new SolidBrush(Color.Black))
                {
                    var format = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };

                    var textRect = new RectangleF(0, targetHeight, targetWidth, textHeight);
                    graphics.DrawString(text, font, brush, textRect, format);
                }
            }

            return finalBitmap;
        }

        private byte[] GenerateSimpleBarcode(string content, int width, int height)
        {
            try
            {
                // Clean the content but don't normalize length
                string cleanedContent = CleanBarcode(content);

                // Simplified fallback method
                var writer = new BarcodeWriter
                {
                    Format = BarcodeFormat.CODE_128,
                    Options = new EncodingOptions
                    {
                        Width = width,
                        Height = height,
                        Margin = 5,
                        PureBarcode = false // Include text in barcode
                    }
                };

                using var bitmap = writer.Write(cleanedContent);

                // Apply threshold to ensure pure black and white
                using var processedBitmap = ApplyThreshold(bitmap);

                using var stream = new MemoryStream();
                processedBitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in simple barcode generation: {ex.Message}");
                throw;
            }
        }

        private Bitmap ApplyThreshold(Bitmap source)
        {
            var result = new Bitmap(source.Width, source.Height);

            for (int x = 0; x < source.Width; x++)
            {
                for (int y = 0; y < source.Height; y++)
                {
                    var pixel = source.GetPixel(x, y);
                    // Convert to pure black or white based on brightness
                    var brightness = (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                    var newColor = brightness < 128 ? Color.Black : Color.White;
                    result.SetPixel(x, y, newColor);
                }
            }

            return result;
        }
    }
}