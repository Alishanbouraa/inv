// Path: QuickTechSystems.WPF.ViewModels/MainStockViewModel.Events.cs
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services;
namespace QuickTechSystems.WPF.ViewModels
{
    public partial class MainStockViewModel
    {
        /// <summary>
        /// Subscribe to events from the event aggregator
        /// </summary>
        protected override void SubscribeToEvents()
        {
            Debug.WriteLine("MainStockViewModel: Subscribing to events");

            _eventAggregator.Subscribe<EntityChangedEvent<MainStockDTO>>(_mainStockChangedHandler);
            _eventAggregator.Subscribe<EntityChangedEvent<CategoryDTO>>(_categoryChangedHandler);
            _eventAggregator.Subscribe<EntityChangedEvent<SupplierDTO>>(_supplierChangedHandler);
            _eventAggregator.Subscribe<EntityChangedEvent<ProductDTO>>(_productChangedHandler);
            _eventAggregator.Subscribe<GlobalDataRefreshEvent>(_globalRefreshHandler);

            Debug.WriteLine("MainStockViewModel: Subscribed to all events");
        }

        /// <summary>
        /// Unsubscribe from all events
        /// </summary>
        protected override void UnsubscribeFromEvents()
        {
            Debug.WriteLine("MainStockViewModel: Unsubscribing from events");

            if (_eventAggregator != null)
            {
                _eventAggregator.Unsubscribe<EntityChangedEvent<MainStockDTO>>(_mainStockChangedHandler);
                _eventAggregator.Unsubscribe<EntityChangedEvent<CategoryDTO>>(_categoryChangedHandler);
                _eventAggregator.Unsubscribe<EntityChangedEvent<SupplierDTO>>(_supplierChangedHandler);
                _eventAggregator.Unsubscribe<EntityChangedEvent<ProductDTO>>(_productChangedHandler);
                _eventAggregator.Unsubscribe<GlobalDataRefreshEvent>(_globalRefreshHandler);
            }

            Debug.WriteLine("MainStockViewModel: Unsubscribed from all events");
        }

        /// <summary>
        /// Handle global refresh event with improved responsiveness
        /// </summary>
        private async void HandleGlobalRefresh(GlobalDataRefreshEvent evt)
        {
            Debug.WriteLine("MainStockViewModel: Handling global refresh event");

            try
            {
                // Use a shorter delay for transfers
                await Task.Delay(100);

                // Try current view refresh first (faster)
                await RefreshCurrentViewAsync();

                Debug.WriteLine("MainStockViewModel: Global refresh completed successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainStockViewModel: Error during global refresh: {ex.Message}");

                // Fallback to full refresh
                try
                {
                    await Task.Delay(200);
                    await RefreshFromDatabaseDirectly();
                }
                catch (Exception fallbackEx)
                {
                    Debug.WriteLine($"MainStockViewModel: Fallback refresh also failed: {fallbackEx.Message}");
                    StatusMessage = "Please click Refresh to update data";
                    await Task.Delay(2000);
                    StatusMessage = "";
                }
            }
        }

        /// <summary>
        /// Handle MainStock entity changes
        /// </summary>
        private async void HandleMainStockChanged(EntityChangedEvent<MainStockDTO> evt)
        {
            try
            {
                Debug.WriteLine($"MainStockViewModel: Handling MainStock change: {evt.Action}");
                await SafeDispatcherOperation(() =>
                {
                    switch (evt.Action)
                    {
                        case "Create":
                            if (!Items.Any(p => p.MainStockId == evt.Entity.MainStockId))
                            {
                                Items.Add(evt.Entity);
                                Debug.WriteLine($"Added new MainStock item {evt.Entity.Name}");
                            }
                            break;

                        case "Update":
                            var existingIndex = Items.ToList().FindIndex(p => p.MainStockId == evt.Entity.MainStockId);
                            if (existingIndex != -1)
                            {
                                Items[existingIndex] = evt.Entity;
                                Debug.WriteLine($"Updated MainStock item {evt.Entity.Name}");
                            }
                            break;

                        case "Delete":
                            var itemToRemove = Items.FirstOrDefault(p => p.MainStockId == evt.Entity.MainStockId);
                            if (itemToRemove != null)
                            {
                                Items.Remove(itemToRemove);
                                Debug.WriteLine($"Removed MainStock item {itemToRemove.Name}");
                            }
                            break;
                    }

                    // Refresh filtered items if we're using search
                    if (!string.IsNullOrWhiteSpace(SearchText))
                    {
                        FilterItems();
                    }
                });
            }
            catch (Exception ex)
            {
                await HandleExceptionWithLogging("MainStock refresh error", ex);
            }
        }

        /// <summary>
        /// Handle Category entity changes
        /// </summary>
        private async void HandleCategoryChanged(EntityChangedEvent<CategoryDTO> evt)
        {
            try
            {
                Debug.WriteLine("MainStockViewModel: Handling category change");
                await SafeDispatcherOperation(() =>
                {
                    switch (evt.Action)
                    {
                        case "Create":
                            // Only add if the category is active
                            if (evt.Entity.IsActive && !Categories.Any(c => c.CategoryId == evt.Entity.CategoryId))
                            {
                                Categories.Add(evt.Entity);
                                Debug.WriteLine($"Added new category {evt.Entity.Name}");
                            }
                            break;
                        case "Update":
                            var existingIndex = Categories.ToList().FindIndex(c => c.CategoryId == evt.Entity.CategoryId);
                            if (existingIndex != -1)
                            {
                                if (evt.Entity.IsActive)
                                {
                                    // Update the existing category if it's active
                                    Categories[existingIndex] = evt.Entity;
                                    Debug.WriteLine($"Updated category {evt.Entity.Name}");
                                }
                                else
                                {
                                    // Remove the category if it's now inactive
                                    Categories.RemoveAt(existingIndex);
                                    Debug.WriteLine($"Removed inactive category {evt.Entity.Name}");
                                }
                            }
                            else if (evt.Entity.IsActive)
                            {
                                // This is a category that wasn't in our list but is now active
                                Categories.Add(evt.Entity);
                                Debug.WriteLine($"Added newly active category {evt.Entity.Name}");
                            }
                            break;
                        case "Delete":
                            var categoryToRemove = Categories.FirstOrDefault(c => c.CategoryId == evt.Entity.CategoryId);
                            if (categoryToRemove != null)
                            {
                                Categories.Remove(categoryToRemove);
                                Debug.WriteLine($"Removed category {categoryToRemove.Name}");
                            }
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                await HandleExceptionWithLogging("Error handling category change", ex);
            }
        }

        /// <summary>
        /// Handle Supplier entity changes
        /// </summary>
        private async void HandleSupplierChanged(EntityChangedEvent<SupplierDTO> evt)
        {
            try
            {
                Debug.WriteLine($"MainStockViewModel: Handling supplier change: {evt.Action}");
                await SafeDispatcherOperation(() =>
                {
                    switch (evt.Action)
                    {
                        case "Create":
                            // Only add if the supplier is active
                            if (evt.Entity.IsActive && !Suppliers.Any(s => s.SupplierId == evt.Entity.SupplierId))
                            {
                                Suppliers.Add(evt.Entity);
                                Debug.WriteLine($"Added new supplier {evt.Entity.Name}");
                            }
                            break;
                        case "Update":
                            var existingIndex = Suppliers.ToList().FindIndex(s => s.SupplierId == evt.Entity.SupplierId);
                            if (existingIndex != -1)
                            {
                                if (evt.Entity.IsActive)
                                {
                                    // Update the existing supplier if it's active
                                    Suppliers[existingIndex] = evt.Entity;
                                    Debug.WriteLine($"Updated supplier {evt.Entity.Name}");
                                }
                                else
                                {
                                    // Remove the supplier if it's now inactive
                                    Suppliers.RemoveAt(existingIndex);
                                    Debug.WriteLine($"Removed inactive supplier {evt.Entity.Name}");
                                }
                            }
                            else if (evt.Entity.IsActive)
                            {
                                // This is a supplier that wasn't in our list but is now active
                                Suppliers.Add(evt.Entity);
                                Debug.WriteLine($"Added newly active supplier {evt.Entity.Name}");
                            }
                            break;
                        case "Delete":
                            var supplierToRemove = Suppliers.FirstOrDefault(s => s.SupplierId == evt.Entity.SupplierId);
                            if (supplierToRemove != null)
                            {
                                Suppliers.Remove(supplierToRemove);
                                Debug.WriteLine($"Removed supplier {supplierToRemove.Name}");
                            }
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                await HandleExceptionWithLogging("Error handling supplier change", ex);
            }
        }

        /// <summary>
        /// Handle Product entity changes
        /// </summary>
        private async void HandleProductChanged(EntityChangedEvent<ProductDTO> evt)
        {
            try
            {
                Debug.WriteLine($"MainStockViewModel: Handling Product change: {evt.Action}");
                await LoadStoreProductsAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionWithLogging("Error handling product change", ex);
            }
        }
    }
}