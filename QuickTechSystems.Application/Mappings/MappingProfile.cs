// Path: QuickTechSystems.Application.Mappings/MappingProfile.cs
using AutoMapper;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Domain.Entities;

namespace QuickTechSystems.Application.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Product Mappings
            CreateMap<Product, ProductDTO>()
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : string.Empty))
                .ForMember(dest => dest.SupplierName, opt => opt.MapFrom(src => src.Supplier != null ? src.Supplier.Name : string.Empty));
            CreateMap<ProductDTO, Product>();
            // Supplier Invoice Mappings
            CreateMap<SupplierInvoice, SupplierInvoiceDTO>()
                .ForMember(dest => dest.SupplierName, opt => opt.MapFrom(src => src.Supplier != null ? src.Supplier.Name : string.Empty))
                .ForMember(dest => dest.Details, opt => opt.MapFrom(src => src.Details));
            CreateMap<SupplierInvoiceDTO, SupplierInvoice>();

            CreateMap<SupplierInvoiceDetail, SupplierInvoiceDetailDTO>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : string.Empty))
                .ForMember(dest => dest.ProductBarcode, opt => opt.MapFrom(src => src.Product != null ? src.Product.Barcode : string.Empty));
            CreateMap<SupplierInvoiceDetailDTO, SupplierInvoiceDetail>();
            // Category Mappings
            CreateMap<Category, CategoryDTO>()
                .ForMember(dest => dest.ProductCount, opt => opt.MapFrom(src => src.Products.Count));
            CreateMap<CategoryDTO, Category>();

            // Customer Mappings
            CreateMap<Customer, CustomerDTO>()
                .ForMember(dest => dest.TransactionCount, opt => opt.MapFrom(src => src.Transactions.Count));
            CreateMap<CustomerDTO, Customer>();
            CreateMap<CustomerDTO, Customer>()
    .ForMember(dest => dest.Balance, opt => opt.MapFrom(src => src.Balance));

            CreateMap<Customer, CustomerDTO>()
                .ForMember(dest => dest.Balance, opt => opt.MapFrom(src => src.Balance));
            // Transaction Mappings
            CreateMap<Transaction, TransactionDTO>()
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Customer != null ? src.Customer.Name : string.Empty))
                .ForMember(dest => dest.Details, opt => opt.MapFrom(src => src.TransactionDetails));
            CreateMap<TransactionDTO, Transaction>()
                .ForMember(dest => dest.TransactionDetails, opt => opt.MapFrom(src => src.Details));
            CreateMap<EmployeeSalaryTransaction, EmployeeSalaryTransactionDTO>()
                .ForMember(dest => dest.EmployeeName,
                    opt => opt.MapFrom(src => $"{src.Employee.FirstName} {src.Employee.LastName}"));
            CreateMap<EmployeeSalaryTransactionDTO, EmployeeSalaryTransaction>();

            // Transaction Detail Mappings
            CreateMap<TransactionDetail, TransactionDetailDTO>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : string.Empty))
                .ForMember(dest => dest.ProductBarcode, opt => opt.MapFrom(src => src.Product != null ? src.Product.Barcode : string.Empty))
                .ForMember(dest => dest.PurchasePrice, opt => opt.MapFrom(src => src.PurchasePrice))
                .ForMember(dest => dest.CategoryId, opt => opt.MapFrom(src => src.Product != null ? src.Product.CategoryId : 0));
            CreateMap<TransactionDetailDTO, TransactionDetail>()
                .ForMember(dest => dest.Product, opt => opt.Ignore());

            // Business and System Settings Mappings
            CreateMap<BusinessSetting, BusinessSettingDTO>();
            CreateMap<BusinessSettingDTO, BusinessSetting>();
            CreateMap<SystemPreference, SystemPreferenceDTO>();
            CreateMap<SystemPreferenceDTO, SystemPreference>();

            // Expense Mappings
            CreateMap<Expense, ExpenseDTO>();
            CreateMap<ExpenseDTO, Expense>();

            // Supplier Related Mappings
            CreateMap<Supplier, SupplierDTO>()
                .ForMember(dest => dest.ProductCount, opt => opt.MapFrom(src => src.Products.Count))
                .ForMember(dest => dest.TransactionCount, opt => opt.MapFrom(src => src.Transactions.Count));
            CreateMap<SupplierDTO, Supplier>();

            CreateMap<SupplierTransaction, SupplierTransactionDTO>()
                .ForMember(dest => dest.SupplierName, opt => opt.MapFrom(src => src.Supplier != null ? src.Supplier.Name : string.Empty));
            CreateMap<SupplierTransactionDTO, SupplierTransaction>();

            // Inventory Mappings
            CreateMap<InventoryHistory, InventoryHistoryDTO>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : string.Empty));
            CreateMap<InventoryHistoryDTO, InventoryHistory>();

            // Drawer Related Mappings
            CreateMap<DrawerTransaction, DrawerTransactionDTO>()
                .ForMember(dest => dest.Timestamp, opt => opt.MapFrom(src => src.Timestamp))
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type))
                .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
                .ForMember(dest => dest.Balance, opt => opt.MapFrom(src => src.Balance))
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
                .ForMember(dest => dest.Notes, opt => opt.MapFrom(src => src.Notes))
                .ForMember(dest => dest.ActionType, opt => opt.MapFrom(src => src.ActionType))
                .ForMember(dest => dest.TransactionReference, opt => opt.MapFrom(src => src.TransactionReference))
                .ForMember(dest => dest.PaymentMethod, opt => opt.MapFrom(src => src.PaymentMethod))
                .ForMember(dest => dest.ResultingBalance, opt => opt.MapFrom(src => src.Balance));
            CreateMap<DrawerTransactionDTO, DrawerTransaction>();

            CreateMap<Drawer, DrawerDTO>()
                .ForMember(dest => dest.DrawerId, opt => opt.MapFrom(src => src.DrawerId))
                .ForMember(dest => dest.OpeningBalance, opt => opt.MapFrom(src => src.OpeningBalance))
                .ForMember(dest => dest.CurrentBalance, opt => opt.MapFrom(src => src.CurrentBalance))
                .ForMember(dest => dest.CashIn, opt => opt.MapFrom(src => src.CashIn))
                .ForMember(dest => dest.CashOut, opt => opt.MapFrom(src => src.CashOut))
                .ForMember(dest => dest.TotalSales, opt => opt.MapFrom(src => src.TotalSales))
                .ForMember(dest => dest.TotalExpenses, opt => opt.MapFrom(src => src.TotalExpenses))
                .ForMember(dest => dest.TotalSupplierPayments, opt => opt.MapFrom(src => src.TotalSupplierPayments))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
                .ForMember(dest => dest.OpenedAt, opt => opt.MapFrom(src => src.OpenedAt))
                .ForMember(dest => dest.ClosedAt, opt => opt.MapFrom(src => src.ClosedAt))
                .ForMember(dest => dest.Notes, opt => opt.MapFrom(src => src.Notes))
                .ForMember(dest => dest.CashierId, opt => opt.MapFrom(src => src.CashierId))
                .ForMember(dest => dest.CashierName, opt => opt.MapFrom(src => src.CashierName))
                .ForMember(dest => dest.LastUpdated, opt => opt.MapFrom(src => src.LastUpdated))
                .ForMember(dest => dest.ExpectedBalance, opt => opt.Ignore())
                .ForMember(dest => dest.Difference, opt => opt.Ignore())
                .ForMember(dest => dest.NetCashflow, opt => opt.Ignore())
                .ForMember(dest => dest.NetSales, opt => opt.Ignore());

            CreateMap<DrawerDTO, Drawer>();

            // Employee Mappings
            CreateMap<Employee, EmployeeDTO>();
            CreateMap<EmployeeDTO, Employee>();

            // Quote Related Mappings
            CreateMap<Quote, QuoteDTO>()
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Customer != null ? src.Customer.Name : string.Empty))
                .ForMember(dest => dest.Details, opt => opt.MapFrom(src => src.QuoteDetails));
            CreateMap<QuoteDTO, Quote>()
                .ForMember(dest => dest.QuoteDetails, opt => opt.MapFrom(src => src.Details));

            CreateMap<QuoteDetail, QuoteDetailDTO>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : string.Empty));
            CreateMap<QuoteDetailDTO, QuoteDetail>();
            // MainStock Mappings
            // Path: QuickTechSystems.Application.Mappings/MappingProfile.cs

            // MainStock Mappings
            CreateMap<MainStock, MainStockDTO>()
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : string.Empty))
                .ForMember(dest => dest.SupplierName, opt => opt.MapFrom(src => src.Supplier != null ? src.Supplier.Name : string.Empty));

            CreateMap<MainStockDTO, MainStock>()
                // Explicitly map stock-related properties to maintain separation
                .ForMember(dest => dest.CurrentStock, opt => opt.MapFrom(src => src.CurrentStock))
                .ForMember(dest => dest.NumberOfBoxes, opt => opt.MapFrom(src => src.NumberOfBoxes))
                .ForMember(dest => dest.ItemsPerBox, opt => opt.MapFrom(src => src.ItemsPerBox));

            // InventoryTransfer Mappings
            CreateMap<InventoryTransfer, InventoryTransferDTO>()
                .ForMember(dest => dest.MainStockName, opt => opt.MapFrom(src => src.MainStock != null ? src.MainStock.Name : string.Empty))
                .ForMember(dest => dest.MainStockBarcode, opt => opt.MapFrom(src => src.MainStock != null ? src.MainStock.Barcode : string.Empty))
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : string.Empty))
                .ForMember(dest => dest.ProductBarcode, opt => opt.MapFrom(src => src.Product != null ? src.Product.Barcode : string.Empty));
            CreateMap<InventoryTransferDTO, InventoryTransfer>();
            // Damaged Goods Mappings
            CreateMap<DamagedGoods, DamagedGoodsDTO>();
            CreateMap<DamagedGoodsDTO, DamagedGoods>();

            CreateMap<LowStockHistory, LowStockHistoryDTO>();
            CreateMap<LowStockHistoryDTO, LowStockHistory>();
            CreateMap<RestaurantTable, RestaurantTableDTO>().ReverseMap();
        }
    }
}