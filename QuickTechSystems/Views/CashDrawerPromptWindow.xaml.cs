using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace QuickTechSystems.WPF.Views
{
    public partial class CashDrawerPromptWindow : Window
    {
        public CashDrawerPromptWindow()
        {
            InitializeComponent();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            var regex = new Regex("[^0-9,.]");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}