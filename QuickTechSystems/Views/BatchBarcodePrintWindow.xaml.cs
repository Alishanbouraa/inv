// Path: QuickTechSystems.WPF.Views/BatchBarcodePrintWindow.xaml.cs
using QuickTechSystems.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QuickTechSystems.WPF.Views
{
    public partial class BatchBarcodePrintWindow : Window
    {
        private List<MainStockDTO> _items;
        private int _labelsPerItem;
        private PrintDialog _printDialog;

        public BatchBarcodePrintWindow(IEnumerable<MainStockDTO> items, int labelsPerItem, PrintDialog printDialog)
        {
            InitializeComponent();
            _items = new List<MainStockDTO>(items);
            _labelsPerItem = labelsPerItem;
            _printDialog = printDialog;

            // Load preview
            LoadPreview();
        }

        private void LoadPreview()
        {
            try
            {
                PreviewTextBlock.Text = $"Preparing to print {_items.Count} items with {_labelsPerItem} labels per item...";

                // Show summary
                SummaryTextBlock.Text = $"Total barcodes to print: {_items.Count * _labelsPerItem}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading preview: {ex.Message}");
                PreviewTextBlock.Text = "Error loading preview";
            }
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Use the print dialog provided
                if (_printDialog != null)
                {
                    // Create scrollViewer to hold the labels
                    var scrollViewer = new ScrollViewer
                    {
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                    };

                    // Create a StackPanel to hold all the barcodes
                    var stackPanel = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };

                    scrollViewer.Content = stackPanel;

                    // Calculate how many labels to fit across the page
                    double pageWidth = _printDialog.PrintableAreaWidth;
                    double labelWidth = 280; // Updated smaller size

                    // Determine how many columns we can fit - now we can fit more
                    int columns = Math.Max(1, (int)(pageWidth / labelWidth));

                    // Current row panel
                    WrapPanel currentRow = null;

                    int labelCount = 0;

                    // Create labels for each item
                    foreach (var item in _items)
                    {
                        for (int i = 0; i < _labelsPerItem; i++)
                        {
                            // Create a new row when needed
                            if (labelCount % columns == 0)
                            {
                                currentRow = new WrapPanel
                                {
                                    Orientation = Orientation.Horizontal,
                                    HorizontalAlignment = HorizontalAlignment.Center
                                };
                                stackPanel.Children.Add(currentRow);
                            }

                            // Create label and add to the current row
                            var label = CreateBarcodeLabel(item);
                            currentRow.Children.Add(label);
                            labelCount++;
                        }
                    }

                    // Print the document
                    _printDialog.PrintVisual(scrollViewer, "Batch Barcode Labels");

                    MessageBox.Show("Printing completed successfully.", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    // Close the window after successful print
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Print dialog is not available.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error printing barcodes: {ex.Message}", "Print Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private UIElement CreateBarcodeLabel(MainStockDTO item)
        {
            // Create a container for the label content
            var canvas = new Canvas
            {
                Width = 280, // Reduced from 380
                Height = 140, // Reduced from 220
                Background = Brushes.White,
                Margin = new Thickness(2) // Smaller margin
            };

            try
            {
                // Add item name (with null check)
                var nameText = item.Name ?? "Unknown Item";
                var nameTextBlock = new TextBlock
                {
                    Text = nameText,
                    FontFamily = new FontFamily("Arial"),
                    FontSize = 9, // Reduced from 12
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Width = 260, // Reduced from 360
                    MaxHeight = 25 // Reduced from 40
                };

                // Position product name at top
                Canvas.SetLeft(nameTextBlock, 10);
                Canvas.SetTop(nameTextBlock, 5); // Reduced from 10
                canvas.Children.Add(nameTextBlock);

                // Position the barcode image - use most of the available space
                double barcodeWidth = 240; // Reduced from 340
                double barcodeHeight = 70; // Reduced from 100

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
                        FontSize = 8, // Reduced from 12
                        TextAlignment = TextAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    };

                    placeholder.Child = placeholderText;

                    // Position placeholder
                    Canvas.SetLeft(placeholder, 20);
                    Canvas.SetTop(placeholder, 35); // Reduced from 55
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

                    // Set high-quality rendering options if available
                    RenderOptions.SetBitmapScalingMode(barcodeImage, BitmapScalingMode.HighQuality);
                    RenderOptions.SetEdgeMode(barcodeImage, EdgeMode.Aliased);

                    Canvas.SetLeft(barcodeImage, 20);
                    Canvas.SetTop(barcodeImage, 35); // Reduced from 55
                    canvas.Children.Add(barcodeImage);
                }

                // Add barcode text (with null check)
                var barcodeText = item.Barcode ?? "No Barcode";
                var barcodeTextBlock = new TextBlock
                {
                    Text = barcodeText,
                    FontFamily = new FontFamily("Arial"),
                    FontSize = 8, // Reduced from 11
                    TextAlignment = TextAlignment.Center,
                    Width = 260 // Reduced from 360
                };

                // Position barcode text below where the barcode image would be
                Canvas.SetLeft(barcodeTextBlock, 10);
                Canvas.SetTop(barcodeTextBlock, 110); // Reduced from 160
                canvas.Children.Add(barcodeTextBlock);

                // Add price if needed
                if (item.SalePrice > 0)
                {
                    var priceTextBlock = new TextBlock
                    {
                        Text = $"${item.SalePrice:N2}",
                        FontFamily = new FontFamily("Arial"),
                        FontSize = 10, // Reduced from 14
                        FontWeight = FontWeights.Bold,
                        TextAlignment = TextAlignment.Center,
                        Width = 260 // Reduced from 360
                    };

                    // Position price at bottom
                    Canvas.SetLeft(priceTextBlock, 10);
                    Canvas.SetTop(priceTextBlock, 125); // Reduced from 185
                    canvas.Children.Add(priceTextBlock);
                }

                // Add a border around the entire label for visual separation
                var border = new Border
                {
                    Width = 280, // Reduced from 380
                    Height = 140, // Reduced from 220
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1)
                };
                Canvas.SetLeft(border, 0);
                Canvas.SetTop(border, 0);
                canvas.Children.Insert(0, border); // Add as first child so it's behind everything else
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
                    Width = 260, // Reduced from 360
                    Foreground = Brushes.Red
                };

                Canvas.SetLeft(errorTextBlock, 10);
                Canvas.SetTop(errorTextBlock, 70); // Adjusted position
                canvas.Children.Add(errorTextBlock);
            }

            return canvas;
        }
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