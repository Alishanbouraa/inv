using System;
using System.Threading.Tasks;
using System.Windows;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using System.Diagnostics;

namespace QuickTechSystems.WPF.ViewModels
{
    public partial class DrawerViewModel
    {
        private async void HandleDrawerUpdate(DrawerUpdateEvent evt)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    // Force a complete refresh for transaction modifications
                    if (evt.Type == "Transaction Modification" ||
                        evt.Type == "Transaction Update")
                    {
                        // Use a short delay to ensure DB operations complete
                        await Task.Delay(200);
                        await RefreshDrawerDataAsync();
                        await LoadFinancialOverviewAsync();
                        UpdateStatus();
                        UpdateTotals();

                        // Force UI refresh
                        OnPropertyChanged(nameof(DrawerHistory));
                    }
                    // For other updates, use the standard refresh
                    else
                    {
                        await RefreshDrawerDataAsync();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error handling drawer update: {ex.Message}");
                    await ShowErrorMessageAsync("Error updating drawer display");
                }
            });
        }

        private async void HandleSupplierPayment(SupplierPaymentEvent evt)
        {
            try
            {
                await LoadDataAsync();
                await LoadFinancialOverviewAsync();
                await LoadDrawerHistoryAsync();
                UpdateStatus();
            }
            catch (Exception ex)
            {
                await ShowErrorMessageAsync($"Error updating drawer after supplier payment: {ex.Message}");
            }
        }

        private async void HandleDrawerChanged(EntityChangedEvent<DrawerDTO> evt)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await LoadDataAsync();
                    await LoadFinancialOverviewAsync();
                    UpdateStatus();
                }
                catch (Exception ex)
                {
                    await ShowErrorMessageAsync($"Error handling drawer update: {ex.Message}");
                }
            });
        }

        protected override void UnsubscribeFromEvents()
        {
            base.UnsubscribeFromEvents();
            _eventAggregator.Unsubscribe<SupplierPaymentEvent>(HandleSupplierPayment);
            _eventAggregator.Unsubscribe<EntityChangedEvent<DrawerDTO>>(HandleDrawerChanged);
            _eventAggregator.Unsubscribe<DrawerUpdateEvent>(HandleDrawerUpdate);
        }

        protected override void SubscribeToEvents()
        {
            base.SubscribeToEvents();

            // Single handler for all drawer-related events
            _eventAggregator.Subscribe<DrawerUpdateEvent>(HandleDrawerUpdate);

            // Handle transaction events
            _eventAggregator.Subscribe<EntityChangedEvent<TransactionDTO>>(async evt =>
            {
                if (evt.Entity != null)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        await RefreshDrawerDataAsync();
                        await LoadFinancialOverviewAsync();
                        UpdateTotals();
                    });
                }
            });
        }

        private async Task HandleDrawerUpdateAsync(DrawerUpdateEvent evt)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await LoadDataSequentiallyAsync();
                }
                catch (Exception ex)
                {
                    await ShowErrorMessageAsync($"Error updating drawer: {ex.Message}");
                }
            });
        }

        private async Task HandleSupplierPaymentAsync(SupplierPaymentEvent evt)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await LoadDataSequentiallyAsync();
                }
                catch (Exception ex)
                {
                    await ShowErrorMessageAsync($"Error handling supplier payment: {ex.Message}");
                }
            });
        }

        private async Task HandleDrawerChangedAsync(EntityChangedEvent<DrawerDTO> evt)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await LoadDataSequentiallyAsync();
                }
                catch (Exception ex)
                {
                    await ShowErrorMessageAsync($"Error handling drawer change: {ex.Message}");
                }
            });
        }

        private async Task HandleDrawerUpdateEvent(DrawerUpdateEvent evt)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await RefreshDrawerDataAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error handling drawer update: {ex.Message}");
                    await ShowErrorMessageAsync("Error updating drawer display");
                }
            });
        }
    }
}