using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Interfaces;
using System.Diagnostics;

namespace QuickTechSystems.WPF.Views
{
    public partial class QuickSupplierInvoiceDialog : Window, IDisposable
    {
        private ISupplierService _supplierService;
        private ISupplierInvoiceService _supplierInvoiceService;
        private IEventAggregator _eventAggregator;
        private bool _isDisposed = false;

        public ObservableCollection<SupplierDTO> Suppliers { get; set; } = new ObservableCollection<SupplierDTO>();
        public DateTime CurrentDate { get; set; } = DateTime.Now;
        public SupplierInvoiceDTO CreatedInvoice { get; private set; }

        public QuickSupplierInvoiceDialog()
        {
            InitializeComponent();

            // Get services from application
            _supplierService = ((App)System.Windows.Application.Current).ServiceProvider.GetService(typeof(ISupplierService)) as ISupplierService;
            _supplierInvoiceService = ((App)System.Windows.Application.Current).ServiceProvider.GetService(typeof(ISupplierInvoiceService)) as ISupplierInvoiceService;
            _eventAggregator = ((App)System.Windows.Application.Current).ServiceProvider.GetService(typeof(IEventAggregator)) as IEventAggregator;

            // Subscribe to supplier change events
            if (_eventAggregator != null)
            {
                _eventAggregator.Subscribe<EntityChangedEvent<SupplierDTO>>(HandleSupplierChanged);
            }

            // Load suppliers
            LoadSuppliersAsync();
            DataContext = this;
        }

        private async void LoadSuppliersAsync()
        {
            try
            {
                var suppliers = await _supplierService.GetActiveAsync();

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Suppliers.Clear();
                    foreach (var supplier in suppliers)
                    {
                        Suppliers.Add(supplier);
                    }
                    Debug.WriteLine($"Loaded {Suppliers.Count} suppliers into dialog");
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading suppliers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HandleSupplierChanged(EntityChangedEvent<SupplierDTO> evt)
        {
            try
            {
                Debug.WriteLine($"QuickSupplierInvoiceDialog: Handling supplier change: {evt.Action} for {evt.Entity.Name}");

                if (evt.Entity.IsActive)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        switch (evt.Action)
                        {
                            case "Create":
                                // Add the new supplier to our collection if it's not already there
                                if (!Suppliers.Any(s => s.SupplierId == evt.Entity.SupplierId))
                                {
                                    Suppliers.Add(evt.Entity);
                                    Debug.WriteLine($"Added new supplier {evt.Entity.Name} to dialog");

                                    // Automatically select the new supplier
                                    SupplierComboBox.SelectedItem = evt.Entity;
                                }
                                break;

                            case "Update":
                                var existingIndex = -1;
                                for (int i = 0; i < Suppliers.Count; i++)
                                {
                                    if (Suppliers[i].SupplierId == evt.Entity.SupplierId)
                                    {
                                        existingIndex = i;
                                        break;
                                    }
                                }

                                if (existingIndex != -1)
                                {
                                    // Check if it was the selected item
                                    bool wasSelected = SupplierComboBox.SelectedItem == Suppliers[existingIndex];

                                    // Update the existing supplier
                                    Suppliers[existingIndex] = evt.Entity;
                                    Debug.WriteLine($"Updated supplier {evt.Entity.Name} in dialog");

                                    // Reselect if it was selected
                                    if (wasSelected)
                                    {
                                        SupplierComboBox.SelectedItem = evt.Entity;
                                    }
                                }
                                else
                                {
                                    // This is a supplier that wasn't in our list but is now active
                                    Suppliers.Add(evt.Entity);
                                    Debug.WriteLine($"Added updated supplier {evt.Entity.Name} to dialog");
                                }
                                break;

                            case "Delete":
                                var supplierToRemove = Suppliers.FirstOrDefault(s => s.SupplierId == evt.Entity.SupplierId);
                                if (supplierToRemove != null)
                                {
                                    bool wasSelected = SupplierComboBox.SelectedItem == supplierToRemove;
                                    Suppliers.Remove(supplierToRemove);
                                    Debug.WriteLine($"Removed supplier {supplierToRemove.Name} from dialog");

                                    // Clear selection if it was selected
                                    if (wasSelected)
                                    {
                                        SupplierComboBox.SelectedItem = null;
                                    }
                                }
                                break;
                        }
                    });
                }
                else if (evt.Action == "Update")
                {
                    // If supplier was set to inactive, remove it from our list
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var supplierToRemove = Suppliers.FirstOrDefault(s => s.SupplierId == evt.Entity.SupplierId);
                        if (supplierToRemove != null)
                        {
                            bool wasSelected = SupplierComboBox.SelectedItem == supplierToRemove;
                            Suppliers.Remove(supplierToRemove);
                            Debug.WriteLine($"Removed inactive supplier {supplierToRemove.Name} from dialog");

                            // Clear selection if it was selected
                            if (wasSelected)
                            {
                                SupplierComboBox.SelectedItem = null;
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"QuickSupplierInvoiceDialog: Error handling supplier change: {ex.Message}");
            }
        }

        private void HeaderPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate inputs
                if (SupplierComboBox.SelectedItem == null)
                {
                    MessageBox.Show("Please select a supplier.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(InvoiceNumberTextBox.Text))
                {
                    MessageBox.Show("Please enter an invoice number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!decimal.TryParse(TotalAmountTextBox.Text, NumberStyles.Currency, CultureInfo.CurrentCulture, out decimal totalAmount) || totalAmount <= 0)
                {
                    MessageBox.Show("Please enter a valid total amount greater than zero.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedSupplier = SupplierComboBox.SelectedItem as SupplierDTO;

                // Create the invoice
                var invoice = new SupplierInvoiceDTO
                {
                    SupplierId = selectedSupplier.SupplierId,
                    SupplierName = selectedSupplier.Name,
                    InvoiceNumber = InvoiceNumberTextBox.Text.Trim(),
                    InvoiceDate = InvoiceDatePicker.SelectedDate ?? DateTime.Now,
                    TotalAmount = totalAmount,
                    Status = "Draft",
                    Notes = NotesTextBox.Text,
                    CreatedAt = DateTime.Now
                };

                // Check if invoice with same number already exists
                var existingInvoice = await _supplierInvoiceService.GetByInvoiceNumberAsync(invoice.InvoiceNumber, invoice.SupplierId);
                if (existingInvoice != null)
                {
                    MessageBox.Show($"An invoice with number '{invoice.InvoiceNumber}' already exists for this supplier.",
                        "Duplicate Invoice", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Save the invoice
                CreatedInvoice = await _supplierInvoiceService.CreateAsync(invoice);

                MessageBox.Show($"Invoice '{CreatedInvoice.InvoiceNumber}' created successfully.",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating invoice: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AddSupplierButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new QuickSupplierDialogWindow
                {
                    Owner = this
                };

                var result = dialog.ShowDialog();
                if (result == true && dialog.NewSupplier != null)
                {
                    var newSupplier = await _supplierService.CreateAsync(dialog.NewSupplier);

                    // We don't need to manually add to Suppliers collection
                    // The event handler will take care of that

                    // But we do want to select the new supplier
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var addedSupplier = Suppliers.FirstOrDefault(s => s.SupplierId == newSupplier.SupplierId);
                        if (addedSupplier != null)
                        {
                            Debug.WriteLine($"Selecting newly added supplier: {addedSupplier.Name}");
                            SupplierComboBox.SelectedItem = addedSupplier;
                        }
                        else
                        {
                            Debug.WriteLine($"Warning: Couldn't find newly added supplier in collection");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding supplier: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Unsubscribe from events
                    if (_eventAggregator != null)
                    {
                        _eventAggregator.Unsubscribe<EntityChangedEvent<SupplierDTO>>(HandleSupplierChanged);
                    }
                }

                _isDisposed = true;
            }
        }
    }
}