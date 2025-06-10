// Path: QuickTechSystems.Application.DTOs/LowStockHistoryDTO.cs
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QuickTechSystems.Application.DTOs
{
    public class LowStockHistoryDTO : BaseDTO, INotifyPropertyChanged
    {
        private int _lowStockHistoryId;
        private int _productId;
        private string _productName = string.Empty;
        private int _currentStock;
        private int _minimumStock;
        private DateTime _alertDate;
        private string _cashierId = string.Empty;
        private string _cashierName = string.Empty;
        private bool _isResolved;
        private DateTime? _resolvedDate;
        private string _resolvedBy = string.Empty;
        private string _notes = string.Empty;

        public int LowStockHistoryId
        {
            get => _lowStockHistoryId;
            set
            {
                _lowStockHistoryId = value;
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

        public string ProductName
        {
            get => _productName;
            set
            {
                _productName = value;
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

        public DateTime AlertDate
        {
            get => _alertDate;
            set
            {
                _alertDate = value;
                OnPropertyChanged();
            }
        }

        public string CashierId
        {
            get => _cashierId;
            set
            {
                _cashierId = value;
                OnPropertyChanged();
            }
        }

        public string CashierName
        {
            get => _cashierName;
            set
            {
                _cashierName = value;
                OnPropertyChanged();
            }
        }

        public bool IsResolved
        {
            get => _isResolved;
            set
            {
                _isResolved = value;
                OnPropertyChanged();
            }
        }

        public DateTime? ResolvedDate
        {
            get => _resolvedDate;
            set
            {
                _resolvedDate = value;
                OnPropertyChanged();
            }
        }

        public string ResolvedBy
        {
            get => _resolvedBy;
            set
            {
                _resolvedBy = value;
                OnPropertyChanged();
            }
        }

        public string Notes
        {
            get => _notes;
            set
            {
                _notes = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}