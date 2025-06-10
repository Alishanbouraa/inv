// Path: QuickTechSystems.WPF.ViewModels/BulkMainStockViewModel.BarcodeOperations.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.WPF.Views;

namespace QuickTechSystems.WPF.ViewModels
{
    public partial class BulkMainStockViewModel
    {
        /// <summary>
        /// Generates barcodes for all items that don't have them.
        /// </summary>


        /// <summary>
        /// Generates a unique barcode for an item.
        /// </summary>
        private string GenerateUniqueBarcode(MainStockDTO item)
        {
            // Use a more reliable uniqueness approach
            var timestamp = DateTime.Now.Ticks.ToString().Substring(10, 8);
            var random = new Random();
            var randomDigits = random.Next(1000, 9999).ToString();
            var categoryPrefix = item.CategoryId > 0 ? item.CategoryId.ToString().PadLeft(3, '0') : "000";

            // Add a checksum digit to improve barcode integrity
            var baseCode = $"{categoryPrefix}{timestamp}{randomDigits}";

            // Simple checksum: sum of all digits modulo 10
            int sum = 0;
            foreach (char c in baseCode)
            {
                if (char.IsDigit(c))
                {
                    sum += (c - '0');
                }
            }
            int checkDigit = sum % 10;

            return $"{baseCode}{checkDigit}";
        }

        /// <summary>
        /// Prints barcodes for all items.
        /// </summary>
        private async Task PrintAllBarcodesAsync()
        {
            try
            {
                StatusMessage = "Preparing barcodes for printing...";
                IsSaving = true;

                var selectedItems = Items.ToList();
                if (!selectedItems.Any())
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("No items available for printing.",
                            "No Items", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                    return;
                }

                if (LabelsPerItem < 1)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("Number of labels must be at least 1.",
                            "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                    return;
                }

                // Generate missing barcodes first
                int generatedCount = 0;
                await Task.Run(() => {
                    foreach (var item in selectedItems)
                    {
                        if (string.IsNullOrWhiteSpace(item.Barcode))
                        {
                            item.Barcode = GenerateUniqueBarcode(item);

                            // Generate box barcode if empty
                            if (string.IsNullOrWhiteSpace(item.BoxBarcode))
                            {
                                item.BoxBarcode = $"BX{item.Barcode}";
                            }

                            generatedCount++;
                        }

                        if (item.BarcodeImage == null)
                        {
                            try
                            {
                                // Using higher resolution barcode generation (600x200)
                                item.BarcodeImage = _barcodeService.GenerateBarcode(item.Barcode, 600, 200);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error generating barcode image: {ex.Message}");
                            }
                        }
                    }
                });

                if (generatedCount > 0)
                {
                    StatusMessage = $"Generated {generatedCount} barcodes...";
                    // Let the UI update
                    await Task.Delay(100);
                }

                bool printerCancelled = false;
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        var printDialog = new System.Windows.Controls.PrintDialog();
                        if (printDialog.ShowDialog() != true)
                        {
                            printerCancelled = true;
                            return;
                        }

                        var batchWindow = new BatchBarcodePrintWindow(selectedItems, LabelsPerItem, printDialog);
                        batchWindow.ShowDialog();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error preparing print window: {ex.Message}",
                            "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });

                if (printerCancelled)
                {
                    StatusMessage = "Printing cancelled by user.";
                    await Task.Delay(1000);
                }
                else
                {
                    StatusMessage = "Barcodes printed successfully.";
                    await Task.Delay(2000);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error printing barcodes: {ex.Message}";
                Debug.WriteLine($"Error printing barcodes: {ex}");

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Error printing barcodes: {ex.Message}",
                        "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                IsSaving = false;
                StatusMessage = string.Empty;
            }
        }

        /// <summary>
        /// Creates a document for barcode printing.
        /// </summary>
        private FixedDocument CreateBarcodeDocument(List<MainStockDTO> items, int labelsPerItem, System.Windows.Controls.PrintDialog printDialog)
        {
            var document = new FixedDocument();
            var pageSize = new Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight);

            // Standard thermal label dimensions
            double deviceIndependentFactor = 96.0;

            // Define exact label size in device-independent pixels
            var labelWidth = 2 * deviceIndependentFactor;    // 2 inches
            var labelHeight = 1 * deviceIndependentFactor;   // 1 inch

            // Use minimal margins for thermal labels
            var margin = new Thickness(0.1 * deviceIndependentFactor);

            // Calculate how many labels can fit on the page
            var labelsPerRow = Math.Max(1, (int)Math.Floor((pageSize.Width - margin.Left - margin.Right) / labelWidth));
            var labelsPerColumn = Math.Max(1, (int)Math.Floor((pageSize.Height - margin.Top - margin.Bottom) / labelHeight));
            var labelsPerPage = labelsPerRow * labelsPerColumn;

            Debug.WriteLine($"Page can fit {labelsPerRow}x{labelsPerColumn} = {labelsPerPage} labels");

            var currentPage = CreateNewPage(pageSize, margin);
            var currentPanel = (WrapPanel)((FixedPage)currentPage.Child).Children[0];
            var labelCount = 0;

            foreach (var item in items)
            {
                for (int i = 0; i < labelsPerItem; i++)
                {
                    if (labelCount >= labelsPerPage)
                    {
                        document.Pages.Add(currentPage);
                        currentPage = CreateNewPage(pageSize, margin);
                        currentPanel = (WrapPanel)((FixedPage)currentPage.Child).Children[0];
                        labelCount = 0;
                    }

                    var labelVisual = CreateBarcodeLabelVisual(item, labelWidth, labelHeight);
                    currentPanel.Children.Add(labelVisual);
                    labelCount++;
                }
            }

            if (labelCount > 0)
            {
                document.Pages.Add(currentPage);
            }

            return document;
        }

        /// <summary>
        /// Creates a new page for the barcode document.
        /// </summary>
        private PageContent CreateNewPage(Size pageSize, Thickness margin)
        {
            var pageContent = new PageContent();
            var fixedPage = new FixedPage();
            fixedPage.Width = pageSize.Width;
            fixedPage.Height = pageSize.Height;

            // Create a WrapPanel to hold the labels
            var wrapPanel = new WrapPanel
            {
                Width = pageSize.Width - margin.Left - margin.Right,
                Height = pageSize.Height - margin.Top - margin.Bottom,
                Margin = margin,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            fixedPage.Children.Add(wrapPanel);
            ((IAddChild)pageContent).AddChild(fixedPage);

            return pageContent;
        }

        /// <summary>
        /// Creates a visual element for a barcode label.
        /// </summary>
        private UIElement CreateBarcodeLabelVisual(MainStockDTO item, double width, double height)
        {
            // Create a container for the label content
            var canvas = new Canvas
            {
                Width = width,
                Height = height,
                Background = Brushes.White,
                Margin = new Thickness(2) // Small margin between labels
            };

            // Position the barcode image - use most of the available space
            double barcodeWidth = width * 0.9;
            double barcodeHeight = height * 0.5;

            try
            {
                // Check if item is null
                if (item == null)
                {
                    throw new ArgumentNullException("item", "Item cannot be null");
                }

                // Load barcode image with null check
                BitmapImage bitmapSource = null;
                if (item.BarcodeImage != null)
                {
                    bitmapSource = LoadBarcodeImage(item.BarcodeImage);
                }

                // Handle case where image didn't load
                if (bitmapSource == null)
                {
                    // Create a placeholder for missing barcode image
                    var placeholder = new Border
                    {
                        Width = barcodeWidth,
                        Height = barcodeHeight,
                        Background = Brushes.LightGray,
                        BorderBrush = Brushes.Gray,
                        BorderThickness = new Thickness(1)
                    };

                    // Add text to placeholder
                    var placeholderText = new TextBlock
                    {
                        Text = "Barcode Image\nNot Available",
                        FontFamily = new FontFamily("Arial"),
                        FontSize = 10,
                        TextAlignment = TextAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    };

                    placeholder.Child = placeholderText;

                    // Position placeholder
                    Canvas.SetLeft(placeholder, (width - barcodeWidth) / 2);
                    Canvas.SetTop(placeholder, height * 0.15);
                    canvas.Children.Add(placeholder);
                }
                else
                {
                    // Create and position barcode image with high-quality rendering
                    var barcodeImage = new Image
                    {
                        Source = bitmapSource,
                        Width = barcodeWidth,
                        Height = barcodeHeight,
                        Stretch = Stretch.Uniform,
                        SnapsToDevicePixels = true
                    };

                    // Set high-quality rendering options
                    RenderOptions.SetBitmapScalingMode(barcodeImage, BitmapScalingMode.HighQuality);
                    RenderOptions.SetEdgeMode(barcodeImage, EdgeMode.Aliased);

                    Canvas.SetLeft(barcodeImage, (width - barcodeWidth) / 2);
                    Canvas.SetTop(barcodeImage, height * 0.15);
                    canvas.Children.Add(barcodeImage);
                }

                // Add item name (with null check)
                var nameText = item.Name ?? "Unknown Item";
                var nameTextBlock = new TextBlock
                {
                    Text = nameText,
                    FontFamily = new FontFamily("Arial"),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Width = width * 0.9,
                    MaxHeight = height * 0.15
                };

                // Position product name at top
                Canvas.SetLeft(nameTextBlock, (width - nameTextBlock.Width) / 2);
                Canvas.SetTop(nameTextBlock, height * 0.02);
                canvas.Children.Add(nameTextBlock);

                // Add barcode text (with null check)
                var barcodeText = item.Barcode ?? "No Barcode";
                var barcodeTextBlock = new TextBlock
                {
                    Text = barcodeText,
                    FontFamily = new FontFamily("Arial"),
                    FontSize = 9,
                    TextAlignment = TextAlignment.Center,
                    Width = width * 0.9
                };

                // Position barcode text below where the barcode image would be
                double barcodeImageBottom = height * 0.15 + barcodeHeight;
                Canvas.SetLeft(barcodeTextBlock, (width - barcodeTextBlock.Width) / 2);
                Canvas.SetTop(barcodeTextBlock, barcodeImageBottom + 5);
                canvas.Children.Add(barcodeTextBlock);

                // Add price if needed
                if (item.SalePrice > 0)
                {
                    var priceTextBlock = new TextBlock
                    {
                        Text = $"${item.SalePrice:N2}",
                        FontFamily = new FontFamily("Arial"),
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        TextAlignment = TextAlignment.Center,
                        Width = width * 0.9
                    };

                    // Position price at bottom
                    Canvas.SetLeft(priceTextBlock, (width - priceTextBlock.Width) / 2);
                    Canvas.SetTop(priceTextBlock, height * 0.8);
                    canvas.Children.Add(priceTextBlock);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating barcode visual: {ex.Message}");

                // Add error message if there's an exception
                var errorTextBlock = new TextBlock
                {
                    Text = $"Error: {ex.Message}",
                    FontFamily = new FontFamily("Arial"),
                    FontSize = 8,
                    TextWrapping = TextWrapping.Wrap,
                    Width = width * 0.9,
                    Foreground = Brushes.Red
                };

                Canvas.SetLeft(errorTextBlock, (width - errorTextBlock.Width) / 2);
                Canvas.SetTop(errorTextBlock, height * 0.8);
                canvas.Children.Add(errorTextBlock);
            }

            // Add a border around the entire label for visual separation
            var border = new Border
            {
                Width = width,
                Height = height,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1)
            };
            Canvas.SetLeft(border, 0);
            Canvas.SetTop(border, 0);
            canvas.Children.Insert(0, border); // Add as first child so it's behind everything else

            return canvas;
        }

        /// <summary>
        /// Loads a barcode image from byte array.
        /// </summary>
        private BitmapImage LoadBarcodeImage(byte[] imageData)
        {
            if (imageData == null) return null;

            var image = new BitmapImage();
            try
            {
                using (var ms = new MemoryStream(imageData))
                {
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = ms;

                    // Add these lines for higher quality
                    image.DecodePixelWidth = 600; // Higher resolution decoding
                    image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;

                    image.EndInit();
                    image.Freeze(); // Important for cross-thread usage
                }
                return image;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading barcode image: {ex.Message}");
                return null;
            }
        }
    }
}