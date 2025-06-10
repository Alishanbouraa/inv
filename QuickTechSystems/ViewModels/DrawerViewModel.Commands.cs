using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using QuickTechSystems.WPF.Views;

namespace QuickTechSystems.WPF.ViewModels
{
    public partial class DrawerViewModel
    {
        public ICommand OpenDrawerCommand { get; }
        public ICommand AddCashCommand { get; }
        public ICommand RemoveCashCommand { get; }
        public ICommand CloseDrawerCommand { get; }
        public ICommand PrintReportCommand { get; }
        public ICommand LoadFinancialDataCommand { get; }
        public ICommand LoadDrawerSessionsCommand { get; }
        public ICommand ApplySessionFilterCommand { get; }
        public ICommand ViewCurrentSessionCommand { get; }

        private async Task OpenDrawerAsync()
        {
            try
            {
                var currentUser = System.Windows.Application.Current.Properties["CurrentUser"] as EmployeeDTO;
                if (currentUser == null)
                {
                    await ShowErrorMessageAsync("No user is currently logged in");
                    return;
                }

                var window = _windowService.GetCurrentWindow();
                var dialog = new InputDialog("Open Drawer", "Enter opening balance:")
                {
                    Owner = window
                };

                if (dialog.ShowDialog() == true && decimal.TryParse(dialog.Input, out decimal amount))
                {
                    CurrentDrawer = await _drawerService.OpenDrawerAsync(
                        amount,
                        currentUser.EmployeeId.ToString(),
                        $"{currentUser.FirstName} {currentUser.LastName}"
                    );
                    await LoadDrawerHistoryAsync();
                    UpdateStatus();
                    MessageBox.Show("Drawer opened successfully.", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                await ShowErrorMessageAsync($"Error opening drawer: {ex.Message}");
            }
        }
        private async Task LoadDrawerSessionsAsync()
        {
            try
            {
                IsProcessing = true;

                var sessions = await _drawerService.GetAllDrawerSessionsAsync(SessionStartDate, SessionEndDate);

                var sessionItems = sessions.Select(s => new DrawerSessionItem
                {
                    DrawerId = s.DrawerId,
                    OpenedAt = s.OpenedAt,
                    ClosedAt = s.ClosedAt,
                    CashierName = s.CashierName,
                    CashierId = s.CashierId,
                    Status = s.Status
                }).ToList();

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    DrawerSessions = new ObservableCollection<DrawerSessionItem>(sessionItems);

                    // Auto-select current session if it exists
                    var currentSession = sessionItems.FirstOrDefault(s => s.Status == "Open");
                    if (currentSession != null)
                    {
                        SelectedDrawerSession = currentSession;
                        IsViewingHistoricalSession = false;
                    }
                });
            }
            catch (Exception ex)
            {
                await ShowErrorMessageAsync($"Error loading drawer sessions: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task ApplySessionFilterAsync()
        {
            try
            {
                if (SessionStartDate > SessionEndDate)
                {
                    await ShowErrorMessageAsync("Start date cannot be after end date");
                    return;
                }

                await LoadDrawerSessionsAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorMessageAsync($"Error applying session filter: {ex.Message}");
            }
        }

        private async Task LoadSelectedSessionAsync()
        {
            if (SelectedDrawerSession == null) return;

            try
            {
                IsProcessing = true;

                // Load the selected drawer session
                var drawer = await _drawerService.GetDrawerSessionByIdAsync(SelectedDrawerSession.DrawerId);
                if (drawer != null)
                {
                    CurrentDrawer = drawer;
                    IsViewingHistoricalSession = drawer.Status != "Open";

                    // Load transaction history for this session
                    await LoadDrawerHistoryAsync();
                    await LoadFinancialOverviewAsync();
                    UpdateStatus();
                }
            }
            catch (Exception ex)
            {
                await ShowErrorMessageAsync($"Error loading selected session: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task ViewCurrentSessionAsync()
        {
            try
            {
                IsProcessing = true;

                // Load current drawer
                CurrentDrawer = await _drawerService.GetCurrentDrawerAsync();
                IsViewingHistoricalSession = false;

                if (CurrentDrawer != null)
                {
                    // Find and select current session in dropdown
                    var currentSessionItem = DrawerSessions?.FirstOrDefault(s => s.DrawerId == CurrentDrawer.DrawerId);
                    if (currentSessionItem != null)
                    {
                        SelectedDrawerSession = currentSessionItem;
                    }
                }

                await LoadDrawerHistoryAsync();
                await LoadFinancialOverviewAsync();
                UpdateStatus();
            }
            catch (Exception ex)
            {
                await ShowErrorMessageAsync($"Error loading current session: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }
        public async Task CloseDrawerWithCurrentBalance()
        {
            try
            {
                IsProcessing = true;

                if (CurrentDrawer == null)
                {
                    await ShowErrorMessageAsync("No active drawer found");
                    return;
                }

                decimal currentBalance = CurrentDrawer.CurrentBalance;
                CurrentDrawer = await _drawerService.CloseDrawerAsync(currentBalance, string.Empty);
                await LoadDrawerHistoryAsync();
                UpdateStatus();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in CloseDrawerWithCurrentBalance: {ex}");
                await ShowErrorMessageAsync($"Error closing drawer: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task AddCashAsync()
        {
            try
            {
                IsProcessing = true;
                var window = _windowService.GetCurrentWindow();
                var dialog = new InputDialog("Add Cash", "Enter amount to add:")
                {
                    Owner = window
                };

                if (dialog.ShowDialog() == true && decimal.TryParse(dialog.Input, out decimal amount))
                {
                    var descDialog = new InputDialog("Add Cash", "Enter a description:")
                    {
                        Owner = window
                    };

                    string description = "Cash added to drawer";
                    if (descDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(descDialog.Input))
                    {
                        description = descDialog.Input;
                    }

                    if (amount <= 0)
                    {
                        await ShowErrorMessageAsync("Amount must be greater than zero");
                        return;
                    }

                    CurrentDrawer = await _drawerService.ProcessTransactionAsync(
                        amount,
                        "Cash In",
                        description);

                    await LoadDrawerHistoryAsync();
                    UpdateStatus();
                    MessageBox.Show($"Successfully added {amount:C2}", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                await ShowErrorMessageAsync($"Error adding cash: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task RemoveCashAsync()
        {
            try
            {
                IsProcessing = true;
                var window = _windowService.GetCurrentWindow();
                var dialog = new InputDialog("Remove Cash", "Enter amount to remove:")
                {
                    Owner = window
                };

                if (dialog.ShowDialog() == true && decimal.TryParse(dialog.Input, out decimal amount))
                {
                    if (amount > CurrentDrawer?.CurrentBalance)
                    {
                        MessageBox.Show("Amount exceeds current balance.", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var descDialog = new InputDialog("Remove Cash", "Enter a reason:")
                    {
                        Owner = window
                    };

                    string description = "Cash removed from drawer";
                    if (descDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(descDialog.Input))
                    {
                        description = descDialog.Input;
                    }

                    if (amount <= 0)
                    {
                        await ShowErrorMessageAsync("Amount must be greater than zero");
                        return;
                    }

                    CurrentDrawer = await _drawerService.ProcessTransactionAsync(
                        amount,
                        "Cash Out",
                        description);

                    await LoadDrawerHistoryAsync();
                    UpdateStatus();
                    MessageBox.Show($"Successfully removed {amount:C2}", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                await ShowErrorMessageAsync($"Error removing cash: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task CloseDrawerAsync()
        {
            try
            {
                IsProcessing = true;
                var window = _windowService.GetCurrentWindow();
                var dialog = new InputDialog("Close Drawer", "Enter final cash amount:")
                {
                    Owner = window
                };

                if (dialog.ShowDialog() == true && decimal.TryParse(dialog.Input, out decimal finalAmount))
                {
                    string notes = await ShowNotesInputDialog();

                    decimal currentBalance = CurrentDrawer?.CurrentBalance ?? 0;
                    CurrentDrawer = await _drawerService.CloseDrawerAsync(finalAmount, notes);
                    await LoadDrawerHistoryAsync();
                    UpdateStatus();

                    decimal difference = finalAmount - currentBalance;

                    var message = difference == 0
                        ? "Drawer closed successfully with no discrepancy."
                        : $"Drawer closed with a {(difference > 0 ? "surplus" : "shortage")} of {Math.Abs(difference):C2}";

                    MessageBox.Show(window, message, "Drawer Closed",
                        MessageBoxButton.OK,
                        difference == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in CloseDrawerAsync: {ex}");
                await ShowErrorMessageAsync($"Error closing drawer: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task<string> ShowNotesInputDialog()
        {
            var window = _windowService.GetCurrentWindow();
            var dialog = new InputDialog("Closing Notes", "Enter any notes for closing:")
            {
                Owner = window
            };

            return dialog.ShowDialog() == true ? dialog.Input : string.Empty;
        }

        public async Task OpenDrawerWithAmount(decimal amount)
        {
            try
            {
                var currentUser = System.Windows.Application.Current.Properties["CurrentUser"] as EmployeeDTO;
                if (currentUser == null)
                {
                    await ShowErrorMessageAsync("No user is currently logged in");
                    return;
                }

                if (amount < 0)
                {
                    await ShowErrorMessageAsync("Amount cannot be negative");
                    return;
                }

                CurrentDrawer = await _drawerService.OpenDrawerAsync(
                    amount,
                    currentUser.EmployeeId.ToString(),
                    $"{currentUser.FirstName} {currentUser.LastName}"
                );
                await LoadDrawerHistoryAsync();
                UpdateStatus();
                MessageBox.Show("Drawer opened successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                await ShowErrorMessageAsync($"Error opening drawer: {ex.Message}");
            }
        }

        public async Task AddCashWithDetails(decimal amount, string description)
        {
            try
            {
                IsProcessing = true;

                if (amount <= 0)
                {
                    await ShowErrorMessageAsync("Amount must be greater than zero");
                    return;
                }

                CurrentDrawer = await _drawerService.ProcessTransactionAsync(
                    amount,
                    "Cash In",
                    string.IsNullOrWhiteSpace(description) ? "Cash added to drawer" : description);

                await LoadDrawerHistoryAsync();
                UpdateStatus();
                MessageBox.Show($"Successfully added {amount:C2}", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                await ShowErrorMessageAsync($"Error adding cash: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        public async Task RemoveCashWithDetails(decimal amount, string description)
        {
            try
            {
                IsProcessing = true;

                if (amount <= 0)
                {
                    await ShowErrorMessageAsync("Amount must be greater than zero");
                    return;
                }

                if (amount > CurrentDrawer?.CurrentBalance)
                {
                    MessageBox.Show("Amount exceeds current balance.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                CurrentDrawer = await _drawerService.ProcessTransactionAsync(
                    amount,
                    "Cash Out",
                    string.IsNullOrWhiteSpace(description) ? "Cash removed from drawer" : description);

                await LoadDrawerHistoryAsync();
                UpdateStatus();
                MessageBox.Show($"Successfully removed {amount:C2}", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                await ShowErrorMessageAsync($"Error removing cash: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        public async Task CloseDrawerWithAmount(decimal finalAmount)
        {
            try
            {
                IsProcessing = true;

                if (finalAmount < 0)
                {
                    await ShowErrorMessageAsync("Final amount cannot be negative");
                    return;
                }

                decimal currentBalance = CurrentDrawer?.CurrentBalance ?? 0;
                CurrentDrawer = await _drawerService.CloseDrawerAsync(finalAmount, string.Empty);
                await LoadDrawerHistoryAsync();
                UpdateStatus();

                decimal difference = finalAmount - currentBalance;

                var message = difference == 0
                    ? "Drawer closed successfully with no discrepancy."
                    : $"Drawer closed with a {(difference > 0 ? "surplus" : "shortage")} of {Math.Abs(difference):C2}";

                MessageBox.Show(message, "Drawer Closed",
                    MessageBoxButton.OK,
                    difference == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in CloseDrawerAsync: {ex}");
                await ShowErrorMessageAsync($"Error closing drawer: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        public async Task PrintReportWithOptions(bool includeTransactions, bool includeFinancialSummary, bool printCashierCopy)
        {
            try
            {
                IsProcessing = true;
                if (CurrentDrawer == null) return;

                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    var document = CreateDrawerReport(includeTransactions, includeFinancialSummary, printCashierCopy);
                    printDialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "Drawer Report");
                }
            }
            catch (Exception ex)
            {
                await ShowErrorMessageAsync($"Error printing report: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }
    }
}