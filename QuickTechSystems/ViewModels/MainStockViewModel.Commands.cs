// Path: QuickTechSystems.WPF.ViewModels/MainStockViewModel.Commands.cs
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using QuickTechSystems.WPF.Commands;
using QuickTechSystems.Application.Services;

namespace QuickTechSystems.WPF.ViewModels
{
    public partial class MainStockViewModel
    {
        private void InitializeCommands()
        {
            try
            {
                Debug.WriteLine("Initializing MainStockViewModel commands");

                LoadCommand = new AsyncRelayCommand(
                    async _ => await LoadDataAsync(),
                    _ => !IsSaving);

                AddCommand = new RelayCommand(
                    _ => AddNew(),
                    _ => !IsSaving);

                SaveCommand = new AsyncRelayCommand(
                    async _ => await SaveAsync(),
                    _ => !IsSaving && SelectedItem != null);

                DeleteCommand = new AsyncRelayCommand(
                    async _ => await DeleteAsync(),
                    _ => !IsSaving && SelectedItem != null);

                GenerateBarcodeCommand = new RelayCommand(
                    _ => GenerateBarcode(),
                    _ => !IsSaving && SelectedItem != null && !string.IsNullOrEmpty(SelectedItem.Barcode));

                GenerateAutomaticBarcodeCommand = new RelayCommand(
                    _ => GenerateAutomaticBarcode(),
                    _ => !IsSaving && SelectedItem != null);

                GenerateMissingBarcodesCommand = new AsyncRelayCommand(
                    async _ => await GenerateMissingBarcodeImages(),
                    _ => !IsSaving);

                UpdateStockCommand = new AsyncRelayCommand(
                    async _ => await UpdateStockAsync(),
                    _ => !IsSaving && SelectedItem != null && StockIncrement > 0);

                PrintBarcodeCommand = new AsyncRelayCommand(
                    async _ => await PrintBarcodeAsync(),
                    _ => !IsSaving && SelectedItem != null && SelectedItem.BarcodeImage != null);

                UploadImageCommand = new RelayCommand(
                    _ => UploadImage(),
                    _ => !IsSaving && SelectedItem != null);

                ClearImageCommand = new RelayCommand(
                    _ => ClearImage(),
                    _ => !IsSaving && SelectedItem != null && !string.IsNullOrEmpty(SelectedItem.ImagePath));

                BulkAddCommand = new AsyncRelayCommand(
                    async _ => {
                        bool success = await ShowBulkAddDialogAndRefresh();
                    },
                    _ => !IsSaving);

                TransferToStoreCommand = new RelayCommand(
                    _ => ShowTransferDialog(),
                    _ => !IsSaving && SelectedItem != null && SelectedItem.CurrentStock > 0);

                SaveTransferCommand = new AsyncRelayCommand(
                    async _ => await TransferToStoreAsync(),
                    _ => !IsSaving && SelectedItem != null && SelectedStoreProduct != null && TransferQuantity > 0);

                // Add the new Box to Individual command
                BoxToIndividualCommand = new AsyncRelayCommand(
                    async _ => await ConvertBoxToIndividualAsync(),
                    _ => !IsSaving && SelectedItem != null && SelectedItem.NumberOfBoxes > 0);

                NextPageCommand = new RelayCommand(
                    _ => CurrentPage++,
                    _ => !IsLastPage && !IsSaving);

                PreviousPageCommand = new RelayCommand(
                    _ => CurrentPage--,
                    _ => !IsFirstPage && !IsSaving);

                GoToPageCommand = new RelayCommand<int>(
                    page => CurrentPage = page,
                    _ => !IsSaving);

                ChangePageSizeCommand = new RelayCommand<int>(
                    size => PageSize = size,
                    _ => !IsSaving);

                Debug.WriteLine("MainStockViewModel commands initialized");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing commands: {ex.Message}");
                throw;
            }
        }
    }
}