// Path: QuickTechSystems.WPF.Views/LowStockHistoryView.xaml.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QuickTechSystems.WPF.Views
{
    public partial class LowStockHistoryView : UserControl
    {
        public LowStockHistoryView()
        {
            InitializeComponent();
        }

        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Clear text selection when clicking anywhere on the control
            ClearTextSelection();
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Additional handler for the main grid to ensure text selection is cleared
            // This helps when clicking empty spaces within the grid
            ClearTextSelection();
        }

        private void ClearTextSelection()
        {
            // Clear keyboard focus which will also clear text selection
            Keyboard.ClearFocus();

            // Ensure DataGrid keeps focus to maintain selection styling
            if (lowStockDataGrid != null)
            {
                lowStockDataGrid.UnselectAllCells();
            }
        }
    }
}