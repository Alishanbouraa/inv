using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.WPF.Commands;

namespace QuickTechSystems.WPF.ViewModels
{
    public class CategoryViewModel : ViewModelBase
    {
        private readonly ICategoryService _categoryService;
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
        private bool _isDisposed;

        private ObservableCollection<CategoryDTO> _productCategories;
        private ObservableCollection<CategoryDTO> _expenseCategories;
        private CategoryDTO? _selectedProductCategory;
        private CategoryDTO? _selectedExpenseCategory;
        private bool _isEditingProduct;
        private bool _isEditingExpense;
        private bool _isLoading;
        private string _loadingMessage;
        private Dictionary<string, string> _validationErrors;
        private Action<EntityChangedEvent<CategoryDTO>> _categoryChangedHandler;

        #region Constructor
        public CategoryViewModel(
            ICategoryService categoryService,
            IEventAggregator eventAggregator) : base(eventAggregator)
        {
            _categoryService = categoryService;
            _productCategories = new ObservableCollection<CategoryDTO>();
            _expenseCategories = new ObservableCollection<CategoryDTO>();
            _validationErrors = new Dictionary<string, string>();
            _categoryChangedHandler = HandleCategoryChanged;
            _loadingMessage = "";

            // Initialize Commands
            AddProductCommand = new RelayCommand(_ => AddProduct());
            AddExpenseCommand = new RelayCommand(_ => AddExpense());
            SaveProductCommand = new AsyncRelayCommand(async _ => await SaveProductAsync());
            SaveExpenseCommand = new AsyncRelayCommand(async _ => await SaveExpenseAsync());
            DeleteProductCommand = new AsyncRelayCommand(async _ => await DeleteProductAsync());
            DeleteExpenseCommand = new AsyncRelayCommand(async _ => await DeleteExpenseAsync());
            RefreshCommand = new AsyncRelayCommand(async _ => await ForceRefreshAsync());

            _ = LoadDataAsync();
        }
        #endregion

        #region Properties
        public ObservableCollection<CategoryDTO> ProductCategories
        {
            get => _productCategories;
            set => SetProperty(ref _productCategories, value);
        }

        public ObservableCollection<CategoryDTO> ExpenseCategories
        {
            get => _expenseCategories;
            set => SetProperty(ref _expenseCategories, value);
        }

        public CategoryDTO? SelectedProductCategory
        {
            get => _selectedProductCategory;
            set
            {
                SetProperty(ref _selectedProductCategory, value);
                IsEditingProduct = value != null;
            }
        }

        public CategoryDTO? SelectedExpenseCategory
        {
            get => _selectedExpenseCategory;
            set
            {
                SetProperty(ref _selectedExpenseCategory, value);
                IsEditingExpense = value != null;
            }
        }

        public bool IsEditingProduct
        {
            get => _isEditingProduct;
            set => SetProperty(ref _isEditingProduct, value);
        }

        public bool IsEditingExpense
        {
            get => _isEditingExpense;
            set => SetProperty(ref _isEditingExpense, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string LoadingMessage
        {
            get => _loadingMessage;
            set => SetProperty(ref _loadingMessage, value);
        }

        public Dictionary<string, string> ValidationErrors
        {
            get => _validationErrors;
            set => SetProperty(ref _validationErrors, value);
        }
        #endregion

        #region Commands
        public ICommand AddProductCommand { get; }
        public ICommand AddExpenseCommand { get; }
        public ICommand SaveProductCommand { get; }
        public ICommand SaveExpenseCommand { get; }
        public ICommand DeleteProductCommand { get; }
        public ICommand DeleteExpenseCommand { get; }
        public ICommand RefreshCommand { get; }
        #endregion

        #region Event Subscriptions
        protected override void SubscribeToEvents()
        {
            _eventAggregator.Subscribe<EntityChangedEvent<CategoryDTO>>(_categoryChangedHandler);
        }

        protected override void UnsubscribeFromEvents()
        {
            _eventAggregator.Unsubscribe<EntityChangedEvent<CategoryDTO>>(_categoryChangedHandler);
        }

        private async void HandleCategoryChanged(EntityChangedEvent<CategoryDTO> evt)
        {
            await LoadDataAsync();
        }
        #endregion

        #region Data Loading
        protected override async Task LoadDataAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                Debug.WriteLine("LoadDataAsync skipped - operation in progress");
                return;
            }

            try
            {
                IsLoading = true;
                LoadingMessage = "Loading categories...";

                var productCategories = await _categoryService.GetByTypeAsync("Product");
                var expenseCategories = await _categoryService.GetByTypeAsync("Expense");

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ProductCategories = new ObservableCollection<CategoryDTO>(productCategories);
                    ExpenseCategories = new ObservableCollection<CategoryDTO>(expenseCategories);

                    // Force property notification
                    OnPropertyChanged(nameof(ProductCategories));
                    OnPropertyChanged(nameof(ExpenseCategories));
                });
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error loading categories: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        private async Task ForceRefreshAsync()
        {
            await LoadDataAsync();
        }
        #endregion

        #region Category Window Management
        public void AddProduct()
        {
            var newCategory = new CategoryDTO
            {
                Type = "Product",
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            OpenCategoryDetailsWindow(newCategory, "Product", true);
        }

        public void AddExpense()
        {
            var newCategory = new CategoryDTO
            {
                Type = "Expense",
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            OpenCategoryDetailsWindow(newCategory, "Expense", true);
        }

        public void EditProductCategory(CategoryDTO category)
        {
            if (category != null)
            {
                SelectedProductCategory = category;
                OpenCategoryDetailsWindow(category, "Product", false);
            }
        }

        public void EditExpenseCategory(CategoryDTO category)
        {
            if (category != null)
            {
                SelectedExpenseCategory = category;
                OpenCategoryDetailsWindow(category, "Expense", false);
            }
        }

        private async void OpenCategoryDetailsWindow(CategoryDTO category, string categoryType, bool isNew)
        {
            // Create a deep copy to avoid modifying the original until save is confirmed
            var categoryCopy = new CategoryDTO
            {
                CategoryId = category.CategoryId,
                Name = category.Name,
                Description = category.Description,
                Type = category.Type,
                IsActive = category.IsActive,
                CreatedAt = category.CreatedAt,
                UpdatedAt = category.UpdatedAt
            };

            var window = new CategoryDetailsWindow(categoryCopy, categoryType, isNew);

            // Show window as dialog
            var result = window.ShowDialog();

            // If dialog result is true (Save was clicked), update the category
            if (result == true)
            {
                // Update the original category with values from the copy
                category.Name = categoryCopy.Name;
                category.Description = categoryCopy.Description;
                category.IsActive = categoryCopy.IsActive;

                // Save changes based on category type
                if (categoryType == "Product")
                {
                    SelectedProductCategory = category;
                    await SaveProductAsync();
                }
                else // Expense
                {
                    SelectedExpenseCategory = category;
                    await SaveExpenseAsync();
                }
            }
        }
        #endregion

        #region Save Operations
        private async Task SaveProductAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Save operation already in progress. Please wait.");
                return;
            }

            try
            {
                if (!ValidateCategory(SelectedProductCategory))
                    return;

                IsLoading = true;
                LoadingMessage = SelectedProductCategory.CategoryId == 0 ?
                    "Creating new product category..." :
                    "Updating product category...";

                // Store a reference to the category being saved
                var categoryBeingSaved = SelectedProductCategory;
                bool isNew = categoryBeingSaved.CategoryId == 0;

                if (isNew)
                {
                    categoryBeingSaved.CreatedAt = DateTime.Now;
                    var savedCategory = await _categoryService.CreateAsync(categoryBeingSaved);
                    await ShowSuccessMessage("Product category created successfully.");

                    // Ensure UI update by adding to collection
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ProductCategories.Add(savedCategory);
                    });
                }
                else
                {
                    categoryBeingSaved.UpdatedAt = DateTime.Now;
                    await _categoryService.UpdateAsync(categoryBeingSaved);
                    await ShowSuccessMessage("Product category updated successfully.");

                    // Update collection item
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        int index = -1;
                        for (int i = 0; i < ProductCategories.Count; i++)
                        {
                            if (ProductCategories[i].CategoryId == categoryBeingSaved.CategoryId)
                            {
                                index = i;
                                break;
                            }
                        }

                        if (index >= 0)
                        {
                            ProductCategories[index] = categoryBeingSaved;
                        }
                    });
                }

                // Always refresh to ensure latest data
                await ForceRefreshAsync();
                SelectedProductCategory = null;
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error saving product category: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        private async Task SaveExpenseAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Save operation already in progress. Please wait.");
                return;
            }

            try
            {
                if (!ValidateCategory(SelectedExpenseCategory))
                    return;

                IsLoading = true;
                LoadingMessage = SelectedExpenseCategory.CategoryId == 0 ?
                    "Creating new expense category..." :
                    "Updating expense category...";

                // Store a reference to the category being saved
                var categoryBeingSaved = SelectedExpenseCategory;
                bool isNew = categoryBeingSaved.CategoryId == 0;

                if (isNew)
                {
                    categoryBeingSaved.CreatedAt = DateTime.Now;
                    var savedCategory = await _categoryService.CreateAsync(categoryBeingSaved);
                    await ShowSuccessMessage("Expense category created successfully.");

                    // Ensure UI update by adding to collection
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ExpenseCategories.Add(savedCategory);
                    });
                }
                else
                {
                    categoryBeingSaved.UpdatedAt = DateTime.Now;
                    await _categoryService.UpdateAsync(categoryBeingSaved);
                    await ShowSuccessMessage("Expense category updated successfully.");

                    // Update collection item
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        int index = -1;
                        for (int i = 0; i < ExpenseCategories.Count; i++)
                        {
                            if (ExpenseCategories[i].CategoryId == categoryBeingSaved.CategoryId)
                            {
                                index = i;
                                break;
                            }
                        }

                        if (index >= 0)
                        {
                            ExpenseCategories[index] = categoryBeingSaved;
                        }
                    });
                }

                // Always refresh to ensure latest data
                await ForceRefreshAsync();
                SelectedExpenseCategory = null;
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error saving expense category: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }
        #endregion

        #region Delete Operations
        private async Task DeleteProductAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Delete operation already in progress. Please wait.");
                return;
            }

            try
            {
                if (SelectedProductCategory == null) return;

                var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    return MessageBox.Show("Are you sure you want to delete this product category?",
                        "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                });

                if (result == MessageBoxResult.Yes)
                {
                    IsLoading = true;
                    LoadingMessage = "Deleting product category...";

                    // Store category ID for reference after deletion
                    int categoryId = SelectedProductCategory.CategoryId;

                    await _categoryService.DeleteAsync(categoryId);

                    // Remove from local collection
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var categoryToRemove = ProductCategories.FirstOrDefault(c => c.CategoryId == categoryId);
                        if (categoryToRemove != null)
                        {
                            ProductCategories.Remove(categoryToRemove);
                        }
                    });

                    await ForceRefreshAsync();
                    SelectedProductCategory = null;
                    await ShowSuccessMessage("Product category deleted successfully.");
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("references") || ex.Message.Contains("constraint"))
                {
                    ShowTemporaryErrorMessage("This category is in use and cannot be deleted. Consider marking it as inactive instead.");

                    // Ask if user wants to mark as inactive instead
                    var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        return MessageBox.Show("Would you like to mark this category as inactive instead?",
                            "Mark as Inactive", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    });

                    if (result == MessageBoxResult.Yes && SelectedProductCategory != null)
                    {
                        SelectedProductCategory.IsActive = false;
                        await SaveProductAsync();
                    }
                }
                else
                {
                    ShowTemporaryErrorMessage($"Error deleting product category: {ex.Message}");
                }
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        private async Task DeleteExpenseAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Delete operation already in progress. Please wait.");
                return;
            }

            try
            {
                if (SelectedExpenseCategory == null) return;

                var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    return MessageBox.Show("Are you sure you want to delete this expense category?",
                        "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                });

                if (result == MessageBoxResult.Yes)
                {
                    IsLoading = true;
                    LoadingMessage = "Deleting expense category...";

                    // Store category ID for reference after deletion
                    int categoryId = SelectedExpenseCategory.CategoryId;

                    await _categoryService.DeleteAsync(categoryId);

                    // Remove from local collection
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var categoryToRemove = ExpenseCategories.FirstOrDefault(c => c.CategoryId == categoryId);
                        if (categoryToRemove != null)
                        {
                            ExpenseCategories.Remove(categoryToRemove);
                        }
                    });

                    await ForceRefreshAsync();
                    SelectedExpenseCategory = null;
                    await ShowSuccessMessage("Expense category deleted successfully.");
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("references") || ex.Message.Contains("constraint"))
                {
                    ShowTemporaryErrorMessage("This category is in use and cannot be deleted. Consider marking it as inactive instead.");

                    // Ask if user wants to mark as inactive instead
                    var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        return MessageBox.Show("Would you like to mark this category as inactive instead?",
                            "Mark as Inactive", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    });

                    if (result == MessageBoxResult.Yes && SelectedExpenseCategory != null)
                    {
                        SelectedExpenseCategory.IsActive = false;
                        await SaveExpenseAsync();
                    }
                }
                else
                {
                    ShowTemporaryErrorMessage($"Error deleting expense category: {ex.Message}");
                }
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }
        #endregion

        #region Validation and Notification
        private bool ValidateCategory(CategoryDTO? category)
        {
            ValidationErrors.Clear();

            if (category == null)
            {
                ValidationErrors.Add("General", "No category selected.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(category.Name))
                ValidationErrors.Add("Name", "Category name is required.");

            if (category.Name?.Length > 100)
                ValidationErrors.Add("Name", "Category name cannot exceed 100 characters.");

            if (category.Description?.Length > 500)
                ValidationErrors.Add("Description", "Description cannot exceed 500 characters.");

            OnPropertyChanged(nameof(ValidationErrors));

            if (ValidationErrors.Count > 0)
            {
                ShowValidationErrors(ValidationErrors.Values.ToList());
                return false;
            }

            return true;
        }

        private void ShowValidationErrors(List<string> errors)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(string.Join("\n", errors), "Validation Errors",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        private async Task ShowSuccessMessage(string message)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        private void ShowTemporaryErrorMessage(string message)
        {
            LoadingMessage = message;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            });

            // Automatically clear error after delay
            Task.Run(async () =>
            {
                await Task.Delay(5000);
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (LoadingMessage == message) // Only clear if still the same message
                    {
                        LoadingMessage = string.Empty;
                    }
                });
            });
        }
        #endregion

        #region IDisposable Implementation
        public override void Dispose()
        {
            if (!_isDisposed)
            {
                _operationLock?.Dispose();
                UnsubscribeFromEvents();

                _isDisposed = true;
            }

            base.Dispose();
        }
        #endregion
    }
}