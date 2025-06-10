using System.Windows;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;

namespace QuickTechSystems.WPF.Views
{
    public partial class CategoryDetailsWindow : Window
    {
        public CategoryDTO Category { get; private set; }
        public string CategoryType { get; private set; }
        public bool IsNewCategory { get; private set; }

        public CategoryDetailsWindow(CategoryDTO category, string categoryType, bool isNewCategory)
        {
            InitializeComponent();

            Category = category;
            CategoryType = categoryType;
            IsNewCategory = isNewCategory;

            // Set DataContext to the category
            DataContext = category;

            // Update UI based on category type and mode
            UpdateUI();
        }

        private void UpdateUI()
        {
            // Set window title and header text based on category type and mode
            string operationText = IsNewCategory ? "ADD NEW" : "EDIT";
            string typeText = CategoryType.ToUpper();

            this.Title = $"{operationText} {typeText} CATEGORY";
            HeaderText.Text = $"{operationText} {typeText} CATEGORY";
            SectionHeader.Text = $"{typeText} CATEGORY INFORMATION";
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CloseButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(Category.Name))
            {
                MessageBox.Show("Category name is required.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            this.DialogResult = true;
            this.Close();
        }
    }
}