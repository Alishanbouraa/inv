using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace QuickTechSystems.WPF.ViewModels
{
    public partial class DrawerViewModel
    {
        private async Task<bool> ValidateTransaction(decimal amount, string transactionType)
        {
            if (CurrentDrawer == null)
            {
                await ShowErrorMessageAsync("No active drawer");
                return false;
            }

            if (amount <= 0)
            {
                await ShowErrorMessageAsync("Amount must be greater than zero");
                return false;
            }

            // For cash out transactions, check if there's enough balance
            if (transactionType.ToLower() is "expense" or "supplier payment" or "return" or "cash out")
            {
                if (CurrentDrawer.CurrentBalance < amount)
                {
                    await ShowErrorMessageAsync("Insufficient funds in drawer");
                    return false;
                }
            }

            return true;
        }

        private decimal CalculateBalance(string transactionType, decimal currentBalance, decimal amount)
        {
            switch (transactionType.ToLower())
            {
                case "open":
                    return amount;
                case "cash sale":
                case "cash in":
                    return currentBalance + amount;
                case "expense":
                case "supplier payment":
                case "return":
                case "cash out":
                    return currentBalance - Math.Abs(amount);
                default:
                    return currentBalance;
            }
        }

        private async Task LoadDrawerHistoryAsync()
        {
            if (CurrentDrawer == null) return;

            int retryCount = 0;
            int maxRetries = 3;
            bool success = false;

            while (!success && retryCount < maxRetries)
            {
                try
                {
                    var history = await _drawerService.GetDrawerHistoryAsync(CurrentDrawer.DrawerId);
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        DrawerHistory = new ObservableCollection<DrawerTransactionDTO>(
                            history.OrderByDescending(t => t.Timestamp)
                        );
                        UpdateTotals();
                        UpdateStatus();
                    });
                    success = true;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    Debug.WriteLine($"Error loading drawer history (attempt {retryCount}): {ex.Message}");

                    if (retryCount >= maxRetries)
                    {
                        await ShowErrorMessageAsync("Unable to load drawer history. Please try refreshing.");
                        DrawerHistory = new ObservableCollection<DrawerTransactionDTO>();
                        ResetFinancialTotals();
                    }
                    else
                    {
                        // Wait briefly before retrying (with increasing delay)
                        await Task.Delay(200 * retryCount);
                    }
                }
            }
        }
        private void UpdateStatus()
        {
            if (CurrentDrawer == null)
            {
                StatusMessage = "No drawer is currently open";
                return;
            }

            var timeInfo = CurrentDrawer.Status == "Open"
                ? $"opened at {CurrentDrawer.OpenedAt:t}"
                : $"closed at {CurrentDrawer.ClosedAt:t}";

            StatusMessage = $"Drawer is {CurrentDrawer.Status.ToLower()} - {CurrentDrawer.CashierName} - {timeInfo}";
            OnPropertyChanged(nameof(StatusMessage));
        }
    }
}