using System.Collections.Generic;
using System.Threading.Tasks;
using QuickTechSystems.Application.DTOs;

namespace QuickTechSystems.Application.Services.Interfaces
{
    public interface ICustomerPrintService
    {
        Task<bool> PrintPaymentHistoryAsync(CustomerDTO customer,
            IEnumerable<TransactionDTO> paymentHistory,
            bool useDateFilter,
            DateTime? startDate = null,
            DateTime? endDate = null);
    }
}