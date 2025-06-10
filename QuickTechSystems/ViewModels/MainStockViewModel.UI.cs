// Path: QuickTechSystems.WPF.ViewModels/MainStockViewModel.UI.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using QuickTechSystems.Application.Services;
namespace QuickTechSystems.WPF.ViewModels
{
    public partial class MainStockViewModel
    {
        /// <summary>
        /// Safely execute an operation on the UI thread with exception handling
        /// </summary>
        private async Task<T> SafeDispatcherOperation<T>(Func<T> action)
        {
            try
            {
                return await System.Windows.Application.Current.Dispatcher.InvokeAsync(action);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in dispatcher operation: {ex.Message}");
                throw new InvalidOperationException("UI operation failed", ex);
            }
        }

        /// <summary>
        /// Safely execute an operation on the UI thread with exception handling (non-return version)
        /// </summary>
        private async Task SafeDispatcherOperation(Action action)
        {
            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(action);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in dispatcher operation: {ex.Message}");
                throw new InvalidOperationException("UI operation failed", ex);
            }
        }

        /// <summary>
        /// Show a temporary error message in the status bar and a message box
        /// </summary>
        private void ShowTemporaryErrorMessage(string message)
        {
            StatusMessage = message;

            SafeDispatcherOperation(() =>
            {
                MessageBox.Show(message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            });

            // Automatically clear error after delay
            Task.Run(async () =>
            {
                await Task.Delay(5000);
                await SafeDispatcherOperation(() =>
                {
                    if (StatusMessage == message) // Only clear if still the same message
                    {
                        StatusMessage = string.Empty;
                    }
                });
            });
        }

        /// <summary>
        /// Handle exception with logging
        /// </summary>
        private async Task HandleExceptionWithLogging(string context, Exception ex)
        {
            string message = $"{context}: {ex.Message}";
            Debug.WriteLine(message);

            if (ex.InnerException != null)
            {
                Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }

            // Only show UI error for user-facing operations
            if (context.Contains("Error") || context.Contains("Failed"))
            {
                await SafeDispatcherOperation(() => StatusMessage = message);
            }
        }

        /// <summary>
        /// Gets the owner window for dialogs
        /// </summary>
        private Window GetOwnerWindow()
        {
            // Try to get the active window first
            var activeWindow = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            if (activeWindow != null)
                return activeWindow;

            // Fall back to the main window
            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow != null && mainWindow.IsLoaded)
                return mainWindow;

            // Last resort, get any window that's visible
            return System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsVisible)
                   ?? System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault();
        }

        /// <summary>
        /// Load a bitmap image from a byte array with proper resource management
        /// </summary>
        private BitmapImage LoadBarcodeImage(byte[] imageData)
        {
            if (imageData == null) return null;

            try
            {
                var image = new BitmapImage();
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

        private bool _autoSyncToProducts = true;

        public bool AutoSyncToProducts
        {
            get => _autoSyncToProducts;
            set => SetProperty(ref _autoSyncToProducts, value);
        }

        /// <summary>
        /// Load image from a file path with proper error handling
        /// </summary>
        private BitmapImage? LoadImageFromPath(string? imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
                return null;

            try
            {
                string fullPath;

                if (_imagePathService != null)
                {
                    fullPath = _imagePathService.GetFullImagePath(imagePath);
                }
                else
                {
                    // Fallback if service not available
                    if (System.IO.Path.IsPathRooted(imagePath))
                    {
                        fullPath = imagePath;
                    }
                    else
                    {
                        fullPath = System.IO.Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "ProductImages",
                            imagePath
                        );
                    }
                }

                if (!System.IO.File.Exists(fullPath))
                {
                    Debug.WriteLine($"Image file not found: {fullPath}");
                    return null;
                }

                // Properly create a file URI
                var image = new BitmapImage();

                // Use using statement for automatic resource cleanup
                using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
                {
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze(); // Important for cross-thread usage
                }

                return image;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading image from path: {ex.Message}");

                // The fallback method with proper error handling
                try
                {
                    // Get the path again since we lost scope
                    string retryPath;
                    if (_imagePathService != null)
                    {
                        retryPath = _imagePathService.GetFullImagePath(imagePath);
                    }
                    else if (System.IO.Path.IsPathRooted(imagePath))
                    {
                        retryPath = imagePath;
                    }
                    else
                    {
                        retryPath = System.IO.Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "ProductImages",
                            imagePath
                        );
                    }

                    // Fallback to stream-based loading with correct parameters
                    var fallbackImage = new BitmapImage();

                    // Use using statement with proper FileStream parameters
                    using (var stream = new System.IO.FileStream(retryPath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                    {
                        fallbackImage.BeginInit();
                        fallbackImage.CacheOption = BitmapCacheOption.OnLoad;
                        fallbackImage.StreamSource = stream;
                        fallbackImage.EndInit();
                        fallbackImage.Freeze();
                    }

                    return fallbackImage;
                }
                catch (Exception fallbackEx)
                {
                    Debug.WriteLine($"Fallback image loading also failed: {fallbackEx.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// Upload an image for the selected item
        /// </summary>
        private void UploadImage()
        {
            // Store current popup state
            bool wasPopupOpen = IsItemPopupOpen;

            // Temporarily close the popup
            if (wasPopupOpen)
            {
                IsItemPopupOpen = false;
            }

            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "Image files (*.jpg, *.jpeg, *.png) | *.jpg; *.jpeg; *.png",
                    Title = "Select image"
                };

                // Get the current owner window
                var ownerWindow = GetOwnerWindow();

                // Show dialog with proper owner
                bool? result = openFileDialog.ShowDialog(ownerWindow);

                if (result == true && SelectedItem != null)
                {
                    try
                    {
                        // Get the source path
                        string sourcePath = openFileDialog.FileName;

                        // Save the image and get the relative path
                        string savedPath = _imagePathService.SaveProductImage(sourcePath);

                        // Set the ImagePath property
                        SelectedItem.ImagePath = savedPath;

                        // Load the image
                        ProductImage = LoadImageFromPath(savedPath);

                        Debug.WriteLine($"Image saved at: {savedPath}");
                    }
                    catch (Exception ex)
                    {
                        ShowTemporaryErrorMessage($"Error loading image: {ex.Message}");
                    }
                }
            }
            finally
            {
                // Restore popup state
                if (wasPopupOpen)
                {
                    IsItemPopupOpen = true;
                }
            }
        }

        /// <summary>
        /// Clear the image for the selected item
        /// </summary>
        private void ClearImage()
        {
            if (SelectedItem != null)
            {
                // If there's an existing image, attempt to delete the file
                if (!string.IsNullOrEmpty(SelectedItem.ImagePath))
                {
                    try
                    {
                        _imagePathService.DeleteProductImage(SelectedItem.ImagePath);

                        // Clear the image path and bitmap
                        SelectedItem.ImagePath = null;
                        ProductImage = null;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error deleting image: {ex.Message}");
                        ShowTemporaryErrorMessage($"Error removing image: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Show the item details popup for editing
        /// </summary>
        // Path: QuickTechSystems.WPF.ViewModels/MainStockViewModel.UI.cs (updated ShowItemPopup method)
        public async void ShowItemPopup()
        {
            try
            {
                // Get ProductService from DI with proper casting
                var app = (App)System.Windows.Application.Current;
                var productService = app.ServiceProvider.GetService<IProductService>();

                // Create the ViewModel for the dialog
                var viewModel = new EditMainStockViewModel(
                    _mainStockService,
                    _categoryService,
                    _supplierService,
                    _supplierInvoiceService,
                    _barcodeService,
                    _imagePathService,
                    productService,
                    _eventAggregator);

                // Initialize with the selected item or new item
                await viewModel.InitializeAsync(SelectedItem);

                // Create and show the dialog
                var dialog = new EditMainStockDialog
                {
                    DataContext = viewModel,
                    Owner = GetOwnerWindow()
                };

                var result = dialog.ShowDialog();

                if (result == true)
                {
                    // Refresh the data to show the changes
                    await SafeLoadDataAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing EditMainStock dialog: {ex.Message}");
                ShowTemporaryErrorMessage($"Error displaying item details: {ex.Message}");
            }
        }

        // Also add or update the EditItem method
        public async void EditItem(MainStockDTO item)
        {
            if (item != null)
            {
                SelectedItem = item;
                IsNewItem = false;
                ShowItemPopup();
            }
        }

        /// <summary>
        /// Handle save completed event from item details window
        /// </summary>
        private void ItemDetailsWindow_SaveCompleted(object sender, RoutedEventArgs e)
        {
            // When save is completed, close the window
            // The window itself already handles closing through the DialogResult
        }

        /// <summary>
        /// Close the item details popup
        /// </summary>
        public void CloseItemPopup()
        {
            // This is no longer needed with Window approach, as the window handles its own closing
            // But we'll keep it for backward compatibility
            IsItemPopupOpen = false;
        }

     
        /// <summary>
        /// Create a UI element for barcode label visualization
        /// </summary>
        private UIElement CreatePrintVisual(MainStockDTO item)
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
    }
}