using System;
using System.Threading.Tasks;
using System.Diagnostics;

namespace QuickTechSystems.WPF.ViewModels
{
    public partial class DrawerViewModel
    {
        public async Task RefreshDrawerDataAsync()
        {
            try
            {
                IsProcessing = true;

                // Get current drawer directly from database
                CurrentDrawer = await _drawerService.GetCurrentDrawerAsync();
                if (CurrentDrawer == null)
                {
                    DrawerHistory.Clear();
                    UpdateStatus();
                    return;
                }

                // Load drawer history transactions
                await LoadDrawerHistoryAsync();

                // Update financial calculations 
                await LoadFinancialOverviewAsync();

                // Make sure to use the database values for balance
                if (CurrentDrawer != null)
                {
                    // Force UI property updates
                    OnPropertyChanged(nameof(CurrentBalance));
                    OnPropertyChanged(nameof(ExpectedBalance));
                    OnPropertyChanged(nameof(Difference));
                }

                UpdateStatus();
                UpdateTotals();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error refreshing drawer data: {ex.Message}");
                await ShowErrorMessageAsync("Error refreshing drawer data");
            }
            finally
            {
                IsProcessing = false;
            }
        }
    }
}