using System.Windows;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;

namespace QuickTechSystems.WPF.Views
{
    public partial class QuickCategoryDialogWindow : Window
    {
        public CategoryDTO NewCategory { get; private set; }

        public QuickCategoryDialogWindow()
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
                MessageBox.Show("Category name is required.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Create a new category DTO
            NewCategory = new CategoryDTO
            {
                Name = NameTextBox.Text.Trim(),
                Type = "Product", // Setting the default type for products
                IsActive = true
            };

            this.DialogResult = true;
            this.Close();
        }
    }
}