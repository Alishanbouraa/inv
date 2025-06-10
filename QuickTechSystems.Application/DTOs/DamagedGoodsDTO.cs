// Path: QuickTechSystems.Application.DTOs/DamagedGoodsDTO.cs
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QuickTechSystems.Application.DTOs
{
    public class DamagedGoodsDTO : INotifyPropertyChanged
    {
        private int _damagedGoodsId;
        private int _productId;
        private string _productName = string.Empty;
        private string _barcode = string.Empty;
        private int _quantity;
        private string _reason = string.Empty;
        private DateTime _dateRegistered;
        private decimal _lossAmount;
        private string _categoryName = string.Empty;
        private bool _isSelected;

        public int DamagedGoodsId
        {
            get => _damagedGoodsId;
            set
            {
                _damagedGoodsId = value;
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

        public string Barcode
        {
            get => _barcode;
            set
            {
                _barcode = value;
                OnPropertyChanged();
            }
        }

        public int Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value;
                OnPropertyChanged();
            }
        }

        public string Reason
        {
            get => _reason;
            set
            {
                _reason = value;
                OnPropertyChanged();
            }
        }

        public DateTime DateRegistered
        {
            get => _dateRegistered;
            set
            {
                _dateRegistered = value;
                OnPropertyChanged();
            }
        }

        public decimal LossAmount
        {
            get => _lossAmount;
            set
            {
                _lossAmount = value;
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

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
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