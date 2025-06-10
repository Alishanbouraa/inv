// Complete updated PrinterService.cs file
// Path: QuickTechSystems.Application/Services/PrinterService.cs
using System.Drawing;
using System.Drawing.Printing;
using QuickTechSystems.Application.Services.Interfaces;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace QuickTechSystems.Application.Services
{
    public class PrinterService : IPrinterService
    {
        private string _selectedPrinter;

        public PrinterService()
        {
            _selectedPrinter = GetDefaultPrinter();
        }

        public void PrintBarcode(byte[] barcodeImage, string productName, string price, string barcodeText)
        {
            using var printDocument = new PrintDocument();
            printDocument.PrinterSettings.PrinterName = _selectedPrinter;

            // Enhanced paper size settings - matching old quality
            var paperSize = new PaperSize("Custom Label",
                (int)(5.5 * 39.37), // Width: 5.5 inches in hundredths
                (int)(4.0 * 39.37)  // Height: 4.0 inches in hundredths
            );
            printDocument.DefaultPageSettings.PaperSize = paperSize;

            // Set margins to minimum for label printing
            printDocument.DefaultPageSettings.Margins = new Margins(5, 5, 5, 5);

            // Create bitmap from byte array with enhanced quality
            using var ms = new MemoryStream(barcodeImage);
            using var barcodeImg = Image.FromStream(ms);

            printDocument.PrintPage += (sender, e) =>
            {
                if (e.Graphics == null) return;

                // Enhanced graphics settings for high quality printing
                e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
                e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor; // Critical for barcodes
                e.Graphics.SmoothingMode = SmoothingMode.None; // No smoothing for barcodes
                e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.None;

                // Calculate positions with REDUCED top padding - moved everything up
                int startX = 20;
                int startY = 10; // REDUCED from 25 to 10 - moves everything up
                int labelWidth = e.MarginBounds.Width - 40;
                int labelHeight = e.MarginBounds.Height - 20; // Also reduced bottom space

                // Print product name with enhanced quality
                using (var nameFont = new Font("Arial", 10, FontStyle.Bold))
                {
                    var nameFormat = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Near,
                        Trimming = StringTrimming.Word
                    };

                    var nameRect = new Rectangle(startX, startY, labelWidth, 30);
                    e.Graphics.DrawString(productName, nameFont, Brushes.Black, nameRect, nameFormat);
                }

                // Calculate barcode position and size
                int barcodeY = startY + 30; // REDUCED spacing from 35 to 30
                int barcodeWidth = Math.Min(labelWidth - 20, 300);
                int barcodeHeight = Math.Min(labelHeight / 2, 80);
                int barcodeX = startX + (labelWidth - barcodeWidth) / 2;

                // Print barcode image with high quality settings
                var barcodeRect = new Rectangle(barcodeX, barcodeY, barcodeWidth, barcodeHeight);

                // Use high-quality drawing with proper scaling
                e.Graphics.CompositingMode = CompositingMode.SourceOver;
                e.Graphics.DrawImage(barcodeImg, barcodeRect);

                // Print ACTUAL barcode text below image (FIXED!)
                using (var barcodeFont = new Font("Consolas", 9, FontStyle.Regular))
                {
                    var barcodeFormat = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Near
                    };

                    // Use the ACTUAL barcode text passed in
                    var barcodeTextRect = new Rectangle(startX, barcodeY + barcodeHeight + 5, labelWidth, 20);
                    e.Graphics.DrawString(barcodeText ?? "NO-BARCODE", barcodeFont, Brushes.Black, barcodeTextRect, barcodeFormat);
                }

                // Print price with enhanced formatting
                using (var priceFont = new Font("Arial", 12, FontStyle.Bold))
                {
                    var priceFormat = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Near
                    };

                    var priceRect = new Rectangle(startX, barcodeY + barcodeHeight + 30, labelWidth, 25); // REDUCED spacing from 35 to 30
                    e.Graphics.DrawString(price, priceFont, Brushes.Black, priceRect, priceFormat);
                }
            };

            try
            {
                printDocument.Print();
            }
            catch (Exception ex)
            {
                throw new Exception($"Printing failed: {ex.Message}");
            }
        }

        // Keep the old method for backward compatibility (overload)
        public void PrintBarcode(byte[] barcodeImage, string productName, string price)
        {
            PrintBarcode(barcodeImage, productName, price, "NO-BARCODE");
        }

        public List<string> GetInstalledPrinters()
        {
            return PrinterSettings.InstalledPrinters.Cast<string>().ToList();
        }

        public void SetPrinter(string printerName)
        {
            if (GetInstalledPrinters().Contains(printerName))
            {
                _selectedPrinter = printerName;
            }
            else
            {
                throw new ArgumentException($"Printer '{printerName}' not found.");
            }
        }

        private string GetDefaultPrinter()
        {
            return new PrinterSettings().PrinterName;
        }
    }
}