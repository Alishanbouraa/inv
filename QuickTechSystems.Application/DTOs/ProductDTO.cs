// Path: QuickTechSystems.Application.DTOs/ProductDTO.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QuickTechSystems.Application.DTOs
{
    public class ProductDTO : BaseDTO, INotifyPropertyChanged
    {
        private int _productId;
        private string _barcode = string.Empty;
        private string _name = string.Empty;
        private string? _description;
        private int _categoryId;
        private string _categoryName = string.Empty;
        private int? _supplierId;
        private string _supplierName = string.Empty;
        private decimal _purchasePrice;
        private decimal _salePrice;
        private int _currentStock;
        private int _minimumStock;
        private byte[]? _barcodeImage;
        private bool _isSelected;
        private bool _isSelectedForPrinting;
        private string? _speed;
        private bool _isActive;
        // Change from byte[] to string
        private string? _imagePath;
        private int? _mainStockId;

        // Box-related fields
        private string _boxBarcode = string.Empty;
        private decimal _boxPurchasePrice;
        private decimal _boxSalePrice;
        private int _numberOfBoxes;
        private int _itemsPerBox = 0;
        private int _minimumBoxStock;
        private int _individualItems;
        public int? SupplierInvoiceId { get; set; }
        // Update property to use ImagePath instead of Image
        // Path: QuickTechSystems.Application.DTOs/ProductDTO.cs

        // Add these private fields
        private decimal _wholesalePrice;
        private decimal _boxWholesalePrice;

        // Add these public properties
        public decimal WholesalePrice
        {
            get => _wholesalePrice;
            set
            {
                _wholesalePrice = value;
                OnPropertyChanged();
            }
        }
        public int TotalStock
        {
            get => CurrentStock;
        }
        public decimal BoxWholesalePrice
        {
            get => _boxWholesalePrice;
            set
            {
                _boxWholesalePrice = value;
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
        public int IndividualItems
        {
            get => _individualItems;
            set
            {
                _individualItems = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalStock));
            }
        }

        public int? MainStockId
        {
            get => _mainStockId;
            set
            {
                _mainStockId = value;
                OnPropertyChanged();
            }
        }
        public int ProductId
        {
            get => _productId;
            set
            {
                _productId = value;
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
        public decimal ItemWholesalePrice
        {
            get
            {
                if (ItemsPerBox <= 0) return 0;
                return BoxWholesalePrice / ItemsPerBox;
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
                _purchasePrice = value;
                OnPropertyChanged();
            }
        }

        public decimal SalePrice
        {
            get => _salePrice;
            set
            {
                _salePrice = value;
                OnPropertyChanged();
            }
        }

        public int CurrentStock
        {
            get => _currentStock;
            set
            {
                _currentStock = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalStock));
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
            }
        }

        public int ItemsPerBox
        {
            get => _itemsPerBox;
            set
            {
                if (value <= 0) value = 1; // Ensure at least 1 item per box
                _itemsPerBox = value;
                OnPropertyChanged();
                // Only recalculate item purchase price
                OnPropertyChanged(nameof(ItemPurchasePrice));
            }
        }

        public decimal BoxPurchasePrice
        {
            get => _boxPurchasePrice;
            set
            {
                _boxPurchasePrice = value;
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
                _boxSalePrice = value;
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
        public decimal ItemPurchasePrice
        {
            get
            {
                if (ItemsPerBox <= 0) return 0;
                return BoxPurchasePrice / ItemsPerBox;
            }
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