using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Diagnostics;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Services.Interfaces;
using System.Collections.ObjectModel;

namespace QuickTechSystems.WPF.Views
{
    public partial class SettlePaymentSupplierWindow : Window
    {
        private readonly SupplierInvoiceDTO _invoice;
        private readonly ISupplierInvoiceService _supplierInvoiceService;
        private ObservableCollection<SupplierTransactionDTO> _invoicePayments;
        private decimal _paymentAmount;
        private decimal _remainingAmount;
        private bool _isProcessingPayment = false; // Add flag to prevent multiple processing

        public SupplierInvoiceDTO SelectedInvoice => _invoice;
        public ObservableCollection<SupplierTransactionDTO> InvoicePayments => _invoicePayments;

        public decimal PaymentAmount
        {
            get => _paymentAmount;
            set
            {
                _paymentAmount = value;
                // Add validation logic if needed
            }
        }

        public decimal RemainingAmount => _remainingAmount;

        public SettlePaymentSupplierWindow(SupplierInvoiceDTO invoice,
                                        ISupplierInvoiceService supplierInvoiceService,
                                        ObservableCollection<SupplierTransactionDTO> payments)
        {
            InitializeComponent();
            DataContext = this;

            _invoice = invoice;
            _supplierInvoiceService = supplierInvoiceService;
            _invoicePayments = payments;

            // Calculate remaining amount
            decimal totalPaid = payments?.Sum(p => Math.Abs(p.Amount)) ?? 0;
            _remainingAmount = invoice.TotalAmount - totalPaid;

            // Default to paying the full remaining amount
            _paymentAmount = _remainingAmount;
        }

        private void PaymentAmountTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            // Focus and select all text when loaded
            if (sender is TextBox textBox)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
            else if (e.Key == Key.Enter)
            {
                ProcessPayment();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ProcessPayment_Click(object sender, RoutedEventArgs e)
        {
            ProcessPayment();
        }

        private async void ProcessPayment()
        {
            // Prevent multiple concurrent calls
            if (_isProcessingPayment)
            {
                Debug.WriteLine("Payment already in progress, ignoring duplicate request");
                return;
            }

            _isProcessingPayment = true;

            try
            {
                // Verify window is in proper state
                if (!this.IsLoaded || !this.IsVisible)
                {
                    Debug.WriteLine("Window not in proper state to process payment");
                    MessageBox.Show("The payment window is not ready. Please try again.", "Error",
                                   MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Validate payment amount
                if (PaymentAmount <= 0)
                {
                    MessageBox.Show("Payment amount must be greater than zero.", "Invalid Amount",
                                   MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (PaymentAmount > RemainingAmount)
                {
                    MessageBox.Show($"Payment amount cannot exceed the remaining amount ({RemainingAmount:C}).",
                                   "Invalid Amount", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Debug.WriteLine($"Processing payment of {PaymentAmount:C} for invoice {SelectedInvoice.InvoiceNumber}");

                // Process the payment
                bool success = await _supplierInvoiceService.SettleInvoiceAsync(SelectedInvoice.SupplierInvoiceId, PaymentAmount);

                if (success)
                {
                    Debug.WriteLine("Payment processed successfully");
                    MessageBox.Show("Payment processed successfully.", "Success",
                                   MessageBoxButton.OK, MessageBoxImage.Information);

                    // Safely set DialogResult and close the window
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => {
                        try
                        {
                            if (this.IsLoaded && this.IsVisible && !this.IsDisposed())
                            {
                                this.DialogResult = true;
                                this.Close();
                            }
                        }
                        catch (Exception dialogEx)
                        {
                            Debug.WriteLine($"Error setting DialogResult: {dialogEx.Message}");
                            // Try to close without DialogResult if necessary
                            try { this.Close(); } catch { }
                        }
                    });
                }
                else
                {
                    MessageBox.Show("Failed to process payment. Please try again.", "Error",
                                   MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing payment: {ex}");
                MessageBox.Show($"Error processing payment: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isProcessingPayment = false;
            }
        }
    }

    // Extension method to check if window is disposed
    public static class WindowExtensions
    {
        public static bool IsDisposed(this Window window)
        {
            try
            {
                // If accessing a property throws an exception, the window is likely disposed
                var test = window.ActualWidth;
                return false;
            }
            catch
            {
                return true;
            }
        }
    }
}