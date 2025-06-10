using System;
using System.Windows;
using System.Windows.Controls;
using QuickTechSystems.WPF.ViewModels;
using System.Collections.ObjectModel;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.WPF.Commands;
using System.Threading.Tasks;

namespace QuickTechSystems.WPF.Views
{
    public partial class DrawerView : UserControl
    {
        private DrawerViewModel ViewModel => DataContext as DrawerViewModel;

        public DrawerView()
        {
            InitializeComponent();
            this.Loaded += DrawerView_Loaded;
        }

        private async void DrawerView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                // Use Execute instead of ExecuteAsync since we're dealing with ICommand interface
                ViewModel.LoadDrawerSessionsCommand.Execute(null);

                // Then load current session data
                await ViewModel.RefreshDrawerDataAsync();
                ViewModel.LoadFinancialDataCommand.Execute(null);
            }
        }

        private void TabButton_Click(object sender, RoutedEventArgs e)
        {
            TabCurrentDrawer.Tag = null;
            TabTransactionHistory.Tag = null;
            TabProfitAnalysis.Tag = null;

            CurrentDrawerView.Visibility = Visibility.Collapsed;
            TransactionHistoryView.Visibility = Visibility.Collapsed;
            ProfitAnalysisView.Visibility = Visibility.Collapsed;

            if (sender == TabCurrentDrawer)
            {
                TabCurrentDrawer.Tag = "Selected";
                CurrentDrawerView.Visibility = Visibility.Visible;
            }
            else if (sender == TabTransactionHistory)
            {
                TabTransactionHistory.Tag = "Selected";
                TransactionHistoryView.Visibility = Visibility.Visible;
            }
            else if (sender == TabProfitAnalysis)
            {
                TabProfitAnalysis.Tag = "Selected";
                ProfitAnalysisView.Visibility = Visibility.Visible;
            }
        }

        private void SummaryButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                var summaryWindow = new DrawerSummaryWindow(ViewModel);
                summaryWindow.Owner = Window.GetWindow(this);
                summaryWindow.ShowDialog();
            }
        }

        private void ActionsButton_Click(object sender, RoutedEventArgs e)
        {
            ActionsPopup.IsOpen = true;
        }

        private void OpenDrawerCommand_Execute(object sender, RoutedEventArgs e)
        {
            ActionsPopup.IsOpen = false;
            OpenDrawerPopup.IsOpen = true;
        }

        private void AddCashCommand_Execute(object sender, RoutedEventArgs e)
        {
            ActionsPopup.IsOpen = false;
            AddCashPopup.IsOpen = true;
        }

        private void RemoveCashCommand_Execute(object sender, RoutedEventArgs e)
        {
            ActionsPopup.IsOpen = false;
            RemoveCashPopup.IsOpen = true;
        }

        private async void CloseDrawerCommand_Execute(object sender, RoutedEventArgs e)
        {
            ActionsPopup.IsOpen = false;

            if (ViewModel != null && ViewModel.CurrentDrawer != null)
            {
                decimal currentBalance = ViewModel.CurrentDrawer.CurrentBalance;

                MessageBoxResult result = MessageBox.Show(
                    $"Current cash in drawer: {currentBalance:C2}\n\nAre you sure you want to close the drawer?",
                    "Close Drawer",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await ViewModel.CloseDrawerWithAmount(currentBalance);
                }
            }
        }

        private void PrintReportCommand_Execute(object sender, RoutedEventArgs e)
        {
            ActionsPopup.IsOpen = false;
            PrintReportPopup.IsOpen = true;
        }

        private void RefreshDataCommand_Execute(object sender, RoutedEventArgs e)
        {
            ActionsPopup.IsOpen = false;
            if (ViewModel != null)
            {
                ViewModel.LoadFinancialDataCommand.Execute(null);
            }
        }

        private async void OpenDrawerConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                await ViewModel.OpenDrawerWithAmount(ViewModel.InitialCashAmount);
                OpenDrawerPopup.IsOpen = false;
            }
        }

        private async void AddCashConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                await ViewModel.AddCashWithDetails(ViewModel.CashAmount, ViewModel.CashDescription);
                AddCashPopup.IsOpen = false;
            }
        }

        private async void RemoveCashConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                await ViewModel.RemoveCashWithDetails(ViewModel.CashAmount, ViewModel.CashDescription);
                RemoveCashPopup.IsOpen = false;
            }
        }

        private async void CloseDrawerConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                await ViewModel.CloseDrawerWithAmount(ViewModel.FinalCashAmount);
                CloseDrawerPopup.IsOpen = false;
            }
        }

        private async void PrintReportConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                await ViewModel.PrintReportWithOptions(
                    ViewModel.IncludeTransactionDetails,
                    ViewModel.IncludeFinancialSummary,
                    ViewModel.PrintCashierCopy);
                PrintReportPopup.IsOpen = false;
            }
        }

        private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            var row = e.Row;
            var item = row.DataContext as DrawerTransactionDTO;
            if (item != null)
            {
                switch (item.Type?.ToLower())
                {
                    case "cash sale":
                    case "return":
                    case "expense":
                    case "supplier payment":
                        // Use a hardcoded color instead of looking for a resource
                        row.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightPink);
                        break;
                }
            }
        }
        private void ClosePopup_Click(object sender, RoutedEventArgs e)
        {
            ActionsPopup.IsOpen = false;
            OpenDrawerPopup.IsOpen = false;
            AddCashPopup.IsOpen = false;
            RemoveCashPopup.IsOpen = false;
            CloseDrawerPopup.IsOpen = false;
            PrintReportPopup.IsOpen = false;
        }
    }
}