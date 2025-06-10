using System.Windows;
using System.Windows.Controls;

namespace QuickTechSystems.WPF.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();

            // Register to the Loaded event to adjust layout based on container size
            this.Loaded += OnControlLoaded;
            this.SizeChanged += OnControlSizeChanged;
        }

        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            AdjustLayoutForSize();
        }

        private void OnControlSizeChanged(object sender, SizeChangedEventArgs e)
        {
            AdjustLayoutForSize();
        }
        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                var viewModel = DataContext as SettingsViewModel;
                var setting = e.Row.Item as BusinessSettingDTO;

                if (viewModel != null && setting != null)
                {
                    // Update the setting directly
                    viewModel.UpdateBusinessSettingDirectEdit(setting);
                }
            }
        }
        private void AdjustLayoutForSize()
        {
            var parentWindow = Window.GetWindow(this);
            if (parentWindow == null) return;

            // Get actual window dimensions
            double windowWidth = parentWindow.ActualWidth;

            // Set margins and paddings based on window size
            var scrollViewer = this.Content as Grid;
            if (scrollViewer == null) return;

            var innerScrollViewer = scrollViewer.Children[0] as ScrollViewer;
            if (innerScrollViewer == null) return;

            var rootGrid = innerScrollViewer.Content as Grid;
            if (rootGrid == null) return;

            if (windowWidth >= 1920) // Large screens
            {
                rootGrid.Margin = new Thickness(32);
            }
            else if (windowWidth >= 1366) // Medium screens
            {
                rootGrid.Margin = new Thickness(24);
            }
            else if (windowWidth >= 800) // Small screens
            {
                rootGrid.Margin = new Thickness(16);
            }
            else // Very small screens
            {
                rootGrid.Margin = new Thickness(8);
            }
        }
    }
}