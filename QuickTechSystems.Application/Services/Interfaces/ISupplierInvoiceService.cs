// Path: QuickTechSystems.Application.Services.Interfaces/ISupplierInvoiceService.cs
using Microsoft.EntityFrameworkCore.Storage;
using QuickTechSystems.Application.DTOs;

namespace QuickTechSystems.Application.Services.Interfaces
{
    public interface ISupplierInvoiceService
    {
        Task<IEnumerable<SupplierInvoiceDTO>> GetAllAsync();
        Task<IEnumerable<SupplierInvoiceDTO>> GetBySupplierAsync(int supplierId);
        Task<SupplierInvoiceDTO?> GetByIdAsync(int invoiceId);
        Task<SupplierInvoiceDTO> CreateAsync(SupplierInvoiceDTO invoiceDto);
        Task UpdateAsync(SupplierInvoiceDTO invoiceDto);
        Task DeleteAsync(int invoiceId);
        Task<IEnumerable<SupplierInvoiceDTO>> GetByStatusAsync(string status);
        Task<bool> ValidateInvoiceAsync(int invoiceId);
        Task<bool> SettleInvoiceAsync(int invoiceId, decimal paymentAmount = 0);
        Task<IEnumerable<SupplierTransactionDTO>> GetInvoicePaymentsAsync(int invoiceId);
        Task<IEnumerable<string>> GetInvoiceNumbersForAutocompleteAsync(string searchTerm);
        Task<SupplierInvoiceDTO?> GetByInvoiceNumberAsync(string invoiceNumber, int supplierId);
        Task UpdateCalculatedAmountAsync(int invoiceId);
        Task AddProductToInvoiceAsync(SupplierInvoiceDetailDTO detailDto);
        Task RemoveProductFromInvoiceAsync(int detailId);
        Task<IEnumerable<SupplierInvoiceDTO>> GetRecentInvoicesAsync(DateTime startDate);
    }
}