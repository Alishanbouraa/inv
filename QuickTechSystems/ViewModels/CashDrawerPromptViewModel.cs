using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.WPF.Commands;

namespace QuickTechSystems.WPF.ViewModels
{
    public class CashDrawerPromptViewModel : ViewModelBase
    {
        private readonly IDrawerService _drawerService;
        private string _cashierName = string.Empty;
        private decimal _openingBalance;
        private string _errorMessage = string.Empty;
        private readonly Window _window;

        public CashDrawerPromptViewModel(
            IDrawerService drawerService,
            Window window,
            IEventAggregator eventAggregator) : base(eventAggregator)
        {
            _drawerService = drawerService;
            _window = window;

            var currentUser = System.Windows.Application.Current.Properties["CurrentUser"] as EmployeeDTO;
            CashierName = currentUser?.FullName ?? "Unknown";

            OpenDrawerCommand = new AsyncRelayCommand(async _ => await OpenDrawerAsync());
        }

        public string CashierName
        {
            get => _cashierName;
            set => SetProperty(ref _cashierName, value);
        }

        public decimal OpeningBalance
        {
            get => _openingBalance;
            set => SetProperty(ref _openingBalance, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public ICommand OpenDrawerCommand { get; }

        private async Task OpenDrawerAsync()
        {
            try
            {
                // Change from '<= 0' to '< 0' to allow zero
                if (OpeningBalance < 0)
                {
                    ErrorMessage = "Opening balance cannot be negative";
                    return;
                }

                var currentUser = System.Windows.Application.Current.Properties["CurrentUser"] as EmployeeDTO;
                if (currentUser == null)
                {
                    ErrorMessage = "User information not found";
                    return;
                }

                await _drawerService.OpenDrawerAsync(
                    OpeningBalance,
                    currentUser.EmployeeId.ToString(),
                    currentUser.FullName);

                _window.DialogResult = true;
                _window.Close();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
    }
}