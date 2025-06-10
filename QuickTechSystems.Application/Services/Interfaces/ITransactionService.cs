// src/Backend/Application/Interfaces/ITransactionService.cs

using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuickTechSystems.Application.Services.Interfaces
{
    public interface ITransactionService : IBaseService<TransactionDTO>
    {
        // Existing methods
        Task<TransactionDTO> ProcessSaleAsync(TransactionDTO transactionDto);
        Task<TransactionDTO> ProcessSaleAsync(TransactionDTO transactionDto, int cashierId);
        Task<TransactionDTO> UpdateAsync(TransactionDTO transactionDto);
        Task<bool> DeleteAsync(int transactionId);
        Task<IEnumerable<TransactionDTO>> GetByCustomerAsync(int customerId);
        Task<TransactionDTO> ProcessPaymentTransactionAsync(TransactionDTO transaction);
        Task<int> GetLatestTransactionIdAsync();
        Task<TransactionDTO?> GetLastTransactionAsync();
        Task<IEnumerable<TransactionDTO>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<decimal> GetTransactionSummaryByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<int> GetTransactionCountByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<Dictionary<string, decimal>> GetCategorySalesByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<(IEnumerable<TransactionDTO> Transactions, int TotalCount)> GetByDateRangePagedAsync(DateTime startDate, DateTime endDate, int page, int pageSize, int? categoryId = null, int? employeeId = null);
        Task<decimal> GetTransactionProfitByDateRangeAsync(DateTime startDate, DateTime endDate, int? categoryId = null, int? employeeId = null);
        Task<IEnumerable<TransactionDTO>> GetByTypeAsync(TransactionType type);
        Task<bool> UpdateStatusAsync(int id, TransactionStatus status);
        Task<decimal> GetTotalSalesAsync(DateTime startDate, DateTime endDate);
        Task<IEnumerable<TransactionDTO>> GetByCustomerAndDateRangeAsync(int customerId, DateTime startDate, DateTime endDate);

        // New methods for employee filtering
        Task<IEnumerable<TransactionDTO>> GetByEmployeeAsync(int employeeId);
        Task<IEnumerable<TransactionDTO>> GetByEmployeeAndDateRangeAsync(int employeeId, DateTime startDate, DateTime endDate);
    }
}