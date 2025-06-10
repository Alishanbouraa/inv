using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.WPF.Commands;
using System.Threading;

namespace QuickTechSystems.WPF.ViewModels
{
    public class QuoteViewModel : ViewModelBase
    {
        private readonly IQuoteService _quoteService;
        private readonly ITransactionService _transactionService;
        private readonly ICustomerService _customerService;
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
        private ObservableCollection<QuoteDTO> _quotes;
        private QuoteDTO? _selectedQuote;
        private string _searchText = string.Empty;
        private bool _isLoading;
        private volatile bool _isOperationInProgress;
        private bool _isInitialized;
        private CancellationTokenSource _initializationCts = new CancellationTokenSource();
        private FlowDirection _flowDirection = FlowDirection.LeftToRight;
        public QuoteViewModel(
            IQuoteService quoteService,
            ITransactionService transactionService,
            ICustomerService customerService,
            IEventAggregator eventAggregator) : base(eventAggregator)
        {
            _quoteService = quoteService;
            _transactionService = transactionService;
            _customerService = customerService;
            _quotes = new ObservableCollection<QuoteDTO>();
            _isOperationInProgress = false;

            // Initialize commands
            SaveAsPdfCommand = new AsyncRelayCommand(async _ => await SafeExecuteOperationAsync(SaveAsPdfAsync), _ => CanExecuteQuoteAction());
            ConvertToCashCommand = new AsyncRelayCommand(async _ => await SafeExecuteOperationAsync(ConvertToCashAsync), _ => CanExecuteQuoteAction());
            RefreshCommand = new AsyncRelayCommand(async _ => await SafeExecuteOperationAsync(LoadDataAsync));
            SearchCommand = new AsyncRelayCommand(async _ => await SafeExecuteOperationAsync(SearchQuotesAsync));

            // Use a background thread for delayed initialization to avoid blocking the UI
            Task.Run(DelayedInitializationAsync);
        }

        private async Task DelayedInitializationAsync()
        {
            try
            {
                // Significant delay to avoid concurrency with other viewmodels during app startup
                await Task.Delay(2000, _initializationCts.Token);

                if (_initializationCts.Token.IsCancellationRequested)
                    return;

                await SafeExecuteOperationAsync(LoadDataAsync);
                _isInitialized = true;
            }
            catch (TaskCanceledException)
            {
                // Initialization was canceled, do nothing
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Delayed initialization failed: {ex}");
            }
        }
        public FlowDirection FlowDirection
        {
            get => _flowDirection;
            set => SetProperty(ref _flowDirection, value);
        }
        private async Task SafeExecuteOperationAsync(Func<Task> operation, int retryCount = 3)
        {
            if (_isOperationInProgress)
            {
                await ShowErrorMessageAsync("An operation is already in progress. Please wait.");
                return;
            }

            // Set flag to indicate operation is in progress
            _isOperationInProgress = true;

            // Try to acquire lock with timeout
            if (!await _operationLock.WaitAsync(TimeSpan.FromSeconds(10)))
            {
                _isOperationInProgress = false;
                await ShowErrorMessageAsync("System is busy. Please try again in a moment.");
                return;
            }

            try
            {
                // Set UI loading state
                IsLoading = true;

                // Wait to ensure no other operations are using the context
                await Task.Delay(1000);

                // Try the operation with retries
                Exception? lastException = null;
                for (int attempt = 0; attempt < retryCount; attempt++)
                {
                    try
                    {
                        // Clear any lingering DbContext references
                        GC.Collect();
                        GC.WaitForPendingFinalizers();

                        // Execute the operation
                        await operation();
                        return; // Success, exit
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("second operation was started"))
                    {
                        // This is the DbContext concurrency error we're trying to handle
                        lastException = ex;
                        Debug.WriteLine($"DbContext concurrency error on attempt {attempt + 1}: {ex.Message}");

                        // Wait longer between retries
                        await Task.Delay((attempt + 1) * 1000);
                    }
                    catch (Exception ex)
                    {
                        // Other exceptions should be reported immediately
                        Debug.WriteLine($"Operation failed: {ex}");
                        await ShowErrorMessageAsync($"Error: {ex.Message}");
                        return;
                    }
                }

                // If we get here, all retry attempts failed
                Debug.WriteLine($"All retry attempts failed: {lastException}");
                await ShowErrorMessageAsync($"Operation failed after {retryCount} attempts: {lastException?.Message}");
            }
            finally
            {
                // Reset states regardless of outcome
                IsLoading = false;
                _isOperationInProgress = false;
                _operationLock.Release();
            }
        }

        public ObservableCollection<QuoteDTO> Quotes
        {
            get => _quotes;
            set => SetProperty(ref _quotes, value);
        }

        public QuoteDTO? SelectedQuote
        {
            get => _selectedQuote;
            set => SetProperty(ref _selectedQuote, value);
        }

        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ICommand SaveAsPdfCommand { get; }
        public ICommand ConvertToCashCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand SearchCommand { get; }

        protected override async Task LoadDataAsync()
        {
            // Wrap in try-catch to handle all possible exceptions
            try
            {
                // Completely detach this method from any previous context by switching to a background thread
                await Task.Run(async () =>
                {
                    try
                    {
                        // Create a separate timeout for just this operation
                        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                        {
                            List<QuoteDTO> pendingQuotes;

                            try
                            {
                                pendingQuotes = (await _quoteService.GetPendingQuotes()).ToList();
                            }
                            catch (InvalidOperationException)
                            {
                                // If we get the concurrency error, wait and try one more time
                                await Task.Delay(2000, cts.Token);
                                pendingQuotes = (await _quoteService.GetPendingQuotes()).ToList();
                            }

                            // Return to UI thread to update UI
                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                try
                                {
                                    // Create a new collection to avoid reference issues
                                    var newQuotes = new ObservableCollection<QuoteDTO>();

                                    // Deep copy each quote to completely detach from EF tracking
                                    foreach (var quote in pendingQuotes)
                                    {
                                        var newQuote = new QuoteDTO
                                        {
                                            QuoteId = quote.QuoteId,
                                            QuoteNumber = quote.QuoteNumber,
                                            CustomerId = quote.CustomerId,
                                            CustomerName = quote.CustomerName,
                                            TotalAmount = quote.TotalAmount,
                                            CreatedDate = quote.CreatedDate,
                                            ExpiryDate = quote.ExpiryDate,
                                            Status = quote.Status,
                                            Details = new ObservableCollection<QuoteDetailDTO>()
                                        };

                                        // Deep copy each detail item
                                        foreach (var detail in quote.Details)
                                        {
                                            newQuote.Details.Add(new QuoteDetailDTO
                                            {
                                                QuoteDetailId = detail.QuoteDetailId,
                                                QuoteId = detail.QuoteId,
                                                ProductId = detail.ProductId,
                                                ProductName = detail.ProductName,
                                                UnitPrice = detail.UnitPrice,
                                                Quantity = detail.Quantity,
                                                Total = detail.Total
                                            });
                                        }

                                        newQuotes.Add(newQuote);
                                    }

                                    // Replace the collection
                                    Quotes = newQuotes;
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"UI update failed: {ex}");
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Background load failed: {ex}");
                        await ShowErrorMessageAsync($"Error loading quotes: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadDataAsync outer exception: {ex}");
                await ShowErrorMessageAsync($"Error loading quotes: {ex.Message}");
            }
        }

        private async Task SearchQuotesAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                await LoadDataAsync();
                return;
            }

            try
            {
                // Create a local copy to avoid race conditions
                string searchTerm = SearchText;

                List<QuoteDTO> searchResults;

                // Perform search on a background thread
                await Task.Run(async () =>
                {
                    try
                    {
                        searchResults = (await _quoteService.SearchQuotes(searchTerm)).ToList();

                        // Update UI on the dispatcher thread
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            // Create a new collection
                            var newCollection = new ObservableCollection<QuoteDTO>();

                            // Deep copy each quote
                            foreach (var quote in searchResults)
                            {
                                var newQuote = new QuoteDTO
                                {
                                    QuoteId = quote.QuoteId,
                                    QuoteNumber = quote.QuoteNumber,
                                    CustomerId = quote.CustomerId,
                                    CustomerName = quote.CustomerName,
                                    TotalAmount = quote.TotalAmount,
                                    CreatedDate = quote.CreatedDate,
                                    ExpiryDate = quote.ExpiryDate,
                                    Status = quote.Status,
                                    Details = new ObservableCollection<QuoteDetailDTO>()
                                };

                                // Deep copy each detail
                                foreach (var detail in quote.Details)
                                {
                                    newQuote.Details.Add(new QuoteDetailDTO
                                    {
                                        QuoteDetailId = detail.QuoteDetailId,
                                        QuoteId = detail.QuoteId,
                                        ProductId = detail.ProductId,
                                        ProductName = detail.ProductName,
                                        UnitPrice = detail.UnitPrice,
                                        Quantity = detail.Quantity,
                                        Total = detail.Total
                                    });
                                }

                                newCollection.Add(newQuote);
                            }

                            // Replace the collection
                            Quotes = newCollection;
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Search operation failed: {ex}");
                        await ShowErrorMessageAsync($"Error searching quotes: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SearchQuotesAsync outer exception: {ex}");
                await ShowErrorMessageAsync($"Error searching quotes: {ex.Message}");
            }
        }

        private async Task SaveAsPdfAsync()
        {
            if (SelectedQuote == null) return;

            try
            {
                // Cache needed properties to avoid null reference issues
                int quoteId = SelectedQuote.QuoteId;
                string quoteNumber = SelectedQuote.QuoteNumber;

                byte[] pdfBytes = null;

                // Generate PDF on background thread
                await Task.Run(async () =>
                {
                    pdfBytes = await _quoteService.GenerateQuotePdf(quoteId);
                });

                if (pdfBytes == null)
                {
                    await ShowErrorMessageAsync("Failed to generate PDF data.");
                    return;
                }

                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"Quote_{quoteNumber}.txt",
                    DefaultExt = ".txt",
                    Filter = "Text files (.txt)|*.txt"
                };

                if (dialog.ShowDialog() == true)
                {
                    await File.WriteAllBytesAsync(dialog.FileName, pdfBytes);
                    await ShowSuccessMessage("Quote saved successfully");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveAsPdfAsync failed: {ex}");
                await ShowErrorMessageAsync($"Error generating quote: {ex.Message}");
            }
        }

        private async Task ConvertToCashAsync()
        {
            if (SelectedQuote == null) return;

            try
            {
                // Cache needed properties to avoid null reference issues
                int quoteId = SelectedQuote.QuoteId;
                string quoteNumber = SelectedQuote.QuoteNumber;

                // Validate drawer status first - do this on a background thread
                bool drawerValid = false;

                await Task.Run(async () =>
                {
                    drawerValid = await _quoteService.ValidateQuotePayment(quoteId, "Cash");
                });

                if (!drawerValid)
                {
                    await ShowErrorMessageAsync("No active cash drawer found or insufficient funds");
                    return;
                }

                if (MessageBox.Show("Convert this quote to a cash transaction?", "Confirm Conversion",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    return;
                }

                // Process conversion on background thread
                QuoteDTO convertedQuote = null;

                await Task.Run(async () =>
                {
                    convertedQuote = await _quoteService.ConvertToTransaction(quoteId, "Cash");
                });

                if (convertedQuote == null)
                {
                    await ShowErrorMessageAsync("Failed to convert quote to transaction.");
                    return;
                }

                _eventAggregator.Publish(new DrawerUpdateEvent(
                    "Quote Payment",
                    convertedQuote.TotalAmount,
                    $"Quote #{quoteNumber}"
                ));

                await ShowSuccessMessage("Quote converted to cash transaction and drawer updated successfully");

                // Reload data after conversion
                await Task.Delay(1000); // Give the DB a chance to settle
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ConvertToCashAsync failed: {ex}");
                await ShowErrorMessageAsync($"Error converting quote: {ex.Message}");
            }
        }

        private bool CanExecuteQuoteAction()
        {
            return SelectedQuote != null && !IsLoading && !_isOperationInProgress;
        }

        protected override void SubscribeToEvents()
        {
            _eventAggregator.Subscribe<EntityChangedEvent<QuoteDTO>>(async evt =>
            {
                // Don't respond to events until we're initialized and not busy
                if (!_isInitialized || _isOperationInProgress || IsLoading)
                    return;

                // Add delay to let any ongoing operations complete
                await Task.Delay(2000);

                // Only proceed if we're still in good state
                if (_isOperationInProgress || IsLoading)
                    return;

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await SafeExecuteOperationAsync(LoadDataAsync);
                });
            });
        }

        public override void Dispose()
        {
            // Cancel any pending initialization
           // _initializationCts.Cancel();
            //_initializationCts.Dispose();

            _operationLock?.Dispose();
            base.Dispose();
        }

        private async Task ShowErrorMessageAsync(string message)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(
                    message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            });
        }

        private async Task ShowSuccessMessage(string message)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(
                    message,
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            });
        }
    }
}