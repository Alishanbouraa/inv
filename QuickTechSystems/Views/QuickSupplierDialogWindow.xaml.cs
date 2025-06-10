using System;
using System.Windows;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;

namespace QuickTechSystems.WPF.Views
{
    public partial class QuickSupplierDialogWindow : Window
    {
        public SupplierDTO NewSupplier { get; private set; }

        public QuickSupplierDialogWindow()
        {
            InitializeComponent();

            // Set focus to the name field
            Loaded += (s, e) => NameTextBox.Focus();
        }

        private void HeaderPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("Supplier name is required.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Create a new supplier DTO
            NewSupplier = new SupplierDTO
            {
                Name = NameTextBox.Text.Trim(),
                ContactPerson = !string.IsNullOrWhiteSpace(ContactPersonTextBox.Text) ? ContactPersonTextBox.Text.Trim() : null,
                Phone = !string.IsNullOrWhiteSpace(PhoneTextBox.Text) ? PhoneTextBox.Text.Trim() : null,
                Email = !string.IsNullOrWhiteSpace(EmailTextBox.Text) ? EmailTextBox.Text.Trim() : null,
                TaxNumber = !string.IsNullOrWhiteSpace(TaxNumberTextBox.Text) ? TaxNumberTextBox.Text.Trim() : null,
                Balance = 0,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            this.DialogResult = true;
            this.Close();
        }
    }
}