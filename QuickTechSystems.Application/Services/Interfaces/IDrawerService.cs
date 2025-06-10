using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;
using QuickTechSystems.Application.DTOs;

namespace QuickTechSystems.Application.Services.Interfaces
{
    public interface IDrawerService : IBaseService<DrawerDTO>
    {
        // Core Drawer Operations
        Task<DrawerDTO?> GetCurrentDrawerAsync();
        Task<DrawerDTO> OpenDrawerAsync(decimal openingBalance, string cashierId, string cashierName);
        Task<DrawerDTO> CloseDrawerAsync(decimal finalBalance, string? notes);
        Task<DrawerDTO> AddCashTransactionAsync(decimal amount, bool isIn);
        Task<decimal> GetCurrentBalanceAsync();
        Task<bool> ProcessSupplierInvoiceAsync(decimal amount, string supplierName, string reference);
        // Transaction Processing
        Task<DrawerDTO> AddCashTransactionAsync(decimal amount, bool isIn, string description);
        Task<DrawerDTO> ProcessTransactionAsync(decimal amount, string transactionType, string description, string reference = "");
        Task<DrawerDTO> ProcessExpenseAsync(decimal amount, string expenseType, string description);
        Task<DrawerDTO> ProcessSupplierPaymentAsync(decimal amount, string supplierName, string reference);
        Task<DrawerDTO> ProcessCashSaleAsync(decimal amount, string reference);
        Task<DrawerDTO> ProcessQuotePaymentAsync(decimal amount, string customerName, string quoteNumber);
        // Add this method to the interface
        Task<bool> UpdateDrawerTransactionForModifiedSaleAsync(int transactionId, decimal oldAmount, decimal newAmount, string description);
        // History and Reporting
        Task<IEnumerable<DrawerTransactionDTO>> GetDrawerHistoryAsync(int drawerId);
        Task<IEnumerable<DrawerTransactionDTO>> GetTransactionsByTypeAsync(string transactionType, DateTime startDate, DateTime endDate);
        Task<decimal> GetTotalByTransactionTypeAsync(string transactionType, DateTime startDate, DateTime endDate);
        Task<bool> ProcessCashReceiptAsync(decimal amount, string description);
        // Financial Management
        Task<DrawerDTO> AdjustBalanceAsync(int drawerId, decimal newBalance, string reason);
        Task<(decimal Sales, decimal SupplierPayments, decimal Expenses)>
            GetFinancialSummaryAsync(DateTime startDate, DateTime endDate);

        // Validation and Calculations
        // Add these methods to the IDrawerService interface
        Task<IEnumerable<DrawerDTO>> GetAllDrawerSessionsAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<DrawerDTO?> GetDrawerSessionByIdAsync(int drawerId);
        Task<bool> ValidateTransactionAsync(decimal amount, bool isCashOut = false);
        Task RecalculateDrawerTotalsAsync(int drawerId);

        // Daily Operations
        Task ResetDailyTotalsAsync(int drawerId);
        Task<(decimal Sales, decimal Expenses)> GetDailyTotalsAsync(int drawerId);
        Task UpdateDailyCalculationsAsync(int drawerId);
        // Add these methods to the IDrawerService interface
        // Balance Verification
        Task<bool> VerifyDrawerBalanceAsync(int drawerId);
        Task<decimal> GetExpectedBalanceAsync(int drawerId);
        Task<decimal> GetActualBalanceAsync(int drawerId);
        Task<decimal> GetBalanceDifferenceAsync(int drawerId);

        // Audit and Security
        Task<IEnumerable<DrawerTransactionDTO>> GetDiscrepancyTransactionsAsync(int drawerId);
        Task LogDrawerAuditAsync(int drawerId, string action, string description);
        Task<bool> ValidateDrawerAccessAsync(string cashierId, int drawerId);
    }
}