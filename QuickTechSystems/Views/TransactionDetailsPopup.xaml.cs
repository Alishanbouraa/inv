using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QuickTechSystems.Views
{
    /// <summary>
    /// Interaction logic for TransactionDetailsPopup.xaml
    /// </summary>
    public partial class TransactionDetailsPopup : UserControl
    {
        public TransactionDetailsPopup()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Find the parent window and close it
            var window = Window.GetWindow(this);
            window?.Close();
        }
    }
}