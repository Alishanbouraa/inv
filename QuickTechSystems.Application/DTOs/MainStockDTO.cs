// Path: QuickTechSystems.Application.DTOs/MainStockDTO.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QuickTechSystems.Application.DTOs
{
    public class MainStockDTO : BaseDTO, INotifyPropertyChanged
    {
        private int _mainStockId;
        private string _barcode = string.Empty;
        private string _name = string.Empty;
        private string? _description;
        private int _categoryId;
        private string _categoryName = string.Empty;
        private int? _supplierId;
        private string _supplierName = string.Empty;
        private decimal _purchasePrice;
        private decimal _salePrice;
        private decimal _currentStock;
        private int _minimumStock;
        private byte[]? _barcodeImage;
        private bool _isSelected;
        private bool _isSelectedForPrinting;
        private string? _speed;
        private bool _isActive;
        private string? _imagePath;
        private string _boxBarcode = string.Empty;
        private decimal _boxPurchasePrice;
        private bool _isUpdatingProperties = false;
        private decimal _boxSalePrice;
        private int _numberOfBoxes;
        private int _itemsPerBox = 0;
        private int _minimumBoxStock;
        private int _individualItems;
        public bool AutoSyncToProducts { get; set; } = true;
        public int? SupplierInvoiceId { get; set; }
        // Path: QuickTechSystems.Application.DTOs/MainStockDTO.cs

        // Add these private fields
        private decimal _wholesalePrice;
        private decimal _boxWholesalePrice;

        // Add these public properties
        public decimal WholesalePrice
        {
            get => _wholesalePrice;
            set
            {
                // Ensure non-negative value
                _wholesalePrice = value < 0 ? 0 : value;
                OnPropertyChanged();
            }
        }
        public MainStockDTO()
        {
            PropertyChanged += MainStockDTO_PropertyChanged;
        }
        public decimal BoxWholesalePrice
        {
            get => _boxWholesalePrice;
            set
            {
                // Ensure non-negative value
                _boxWholesalePrice = value < 0 ? 0 : value;
                OnPropertyChanged();
            }
        }
        public string? ImagePath
        {
            get => _imagePath;
            set
            {
                _imagePath = value;
                OnPropertyChanged();
            }
        }
        private void MainStockDTO_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // We only want to update the related property if we're not already
            // in the middle of a property change cascade
            if (!_isUpdatingProperties)
            {
                _isUpdatingProperties = true;
                try
                {
                    // Handle relationships between box and individual prices
                    if (e.PropertyName == nameof(ItemsPerBox))
                    {
                        // Don't recalculate if both values are set by the user
                        if (PurchasePrice > 0 && BoxPurchasePrice > 0)
                            return;

                        // Recalculate appropriate price when items per box changes
                        if (BoxPurchasePrice > 0 && ItemsPerBox > 0)
                            PurchasePrice = Math.Round(BoxPurchasePrice / ItemsPerBox, 2);
                        else if (PurchasePrice > 0 && ItemsPerBox > 0)
                            BoxPurchasePrice = Math.Round(PurchasePrice * ItemsPerBox, 2);
                    }
                }
                finally
                {
                    _isUpdatingProperties = false;
                }
            }
        }

        public int MainStockId
        {
            get => _mainStockId;
            set
            {
                _mainStockId = value;
                OnPropertyChanged();
            }
        }

        public string Barcode
        {
            get => _barcode;
            set
            {
                _barcode = value;
                OnPropertyChanged();
            }
        }
        public int IndividualItems
        {
            get => (int)CurrentStock;
            set
            {
                _individualItems = value;
                // IMPORTANT: Update CurrentStock when IndividualItems is set
                CurrentStock = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentStock));
                OnPropertyChanged(nameof(TotalStock));
            }
        }
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public string? Speed
        {
            get => _speed;
            set
            {
                _speed = value;
                OnPropertyChanged();
            }
        }

        public string? Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }

        public int CategoryId
        {
            get => _categoryId;
            set
            {
                _categoryId = value;
                OnPropertyChanged();
            }
        }
        // Add this property to MainStockDTO.cs
        // Calculated property - Item Wholesale Price
        public decimal ItemWholesalePrice
        {
            get
            {
                if (ItemsPerBox <= 0) return WholesalePrice;
                return BoxWholesalePrice / ItemsPerBox;
            }
        }
        public string CategoryName
        {
            get => _categoryName;
            set
            {
                _categoryName = value;
                OnPropertyChanged();
            }
        }

        public int? SupplierId
        {
            get => _supplierId;
            set
            {
                _supplierId = value;
                OnPropertyChanged();
            }
        }

        public string SupplierName
        {
            get => _supplierName;
            set
            {
                _supplierName = value;
                OnPropertyChanged();
            }
        }

        public decimal PurchasePrice
        {
            get => _purchasePrice;
            set
            {
                // Ensure non-negative value
                _purchasePrice = value < 0 ? 0 : value;
                OnPropertyChanged();
            }
        }

        public decimal SalePrice
        {
            get => _salePrice;
            set
            {
                // Ensure non-negative value
                _salePrice = value < 0 ? 0 : value;
                OnPropertyChanged();
            }
        }
        public decimal CurrentStock
        {
            get => _currentStock;
            set
            {
                _currentStock = value;
                OnPropertyChanged();
            }
        }

        public int MinimumStock
        {
            get => _minimumStock;
            set
            {
                _minimumStock = value;
                OnPropertyChanged();
            }
        }

        public byte[]? BarcodeImage
        {
            get => _barcodeImage;
            set
            {
                _barcodeImage = value;
                OnPropertyChanged();
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public bool IsSelectedForPrinting
        {
            get => _isSelectedForPrinting;
            set
            {
                _isSelectedForPrinting = value;
                OnPropertyChanged();
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                OnPropertyChanged();
            }
        }

        // Box-related properties
        public string BoxBarcode
        {
            get => _boxBarcode;
            set
            {
                _boxBarcode = value;
                OnPropertyChanged();
            }
        }

        public int NumberOfBoxes
        {
            get => _numberOfBoxes;
            set
            {
                _numberOfBoxes = value;
                OnPropertyChanged();
                // Recalculate total stock when number of boxes changes
                OnPropertyChanged(nameof(TotalStock));
            }
        }

        public int ItemsPerBox
        {
            get => _itemsPerBox;
            set
            {
                // Remove the line that forces a minimum value of 1
                // if (value <= 0) value = 1; // Ensure at least 1 item per box
                _itemsPerBox = value;
                OnPropertyChanged();
                // Recalculate item purchase price and total stock
                OnPropertyChanged(nameof(ItemPurchasePrice));
                OnPropertyChanged(nameof(TotalStock));
            }
        }

        public decimal BoxPurchasePrice
        {
            get => _boxPurchasePrice;
            set
            {
                // Ensure non-negative value
                _boxPurchasePrice = value < 0 ? 0 : value;
                OnPropertyChanged();
                // Recalculate item purchase price
                OnPropertyChanged(nameof(ItemPurchasePrice));
            }
        }

        public decimal BoxSalePrice
        {
            get => _boxSalePrice;
            set
            {
                // Ensure non-negative value
                _boxSalePrice = value < 0 ? 0 : value;
                OnPropertyChanged();
            }
        }

        public int MinimumBoxStock
        {
            get => _minimumBoxStock;
            set
            {
                _minimumBoxStock = value;
                OnPropertyChanged();
            }
        }

        // Calculated property - Item Purchase Price
        // Path: QuickTechSystems.Application.DTOs/MainStockDTO.cs
        // Update the ItemPurchasePrice calculated property

        // Calculated property - Item Purchase Price
        public decimal ItemPurchasePrice
        {
            get
            {
                // If user has directly entered a PurchasePrice, return that
                if (PurchasePrice > 0)
                    return PurchasePrice;

                // Otherwise, calculate it from box values if possible
                if (ItemsPerBox <= 0) return 0;
                return BoxPurchasePrice / ItemsPerBox;
            }
        }

        // Calculated property - Total Stock
        // In MainStockDTO.cs
        // Calculated property - Total Stock (for display purposes only)
        public decimal TotalStock
        {
            get => CurrentStock; // Now only shows individual items
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return $"{Name} ({Barcode})";
        }
    }
}