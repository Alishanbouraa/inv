using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuickTechSystems.Application.DTOs;

namespace QuickTechSystems.Application.Services.Interfaces
{
    public interface IQuoteService : IBaseService<QuoteDTO>
    {
        Task<QuoteDTO> CreateQuoteFromTransaction(TransactionDTO transaction);
        Task<QuoteDTO> ConvertToTransaction(int quoteId, string paymentMethod);
        Task<byte[]> GenerateQuotePdf(int quoteId);
        Task<IEnumerable<QuoteDTO>> GetQuotesByCustomer(int customerId);
        Task<IEnumerable<QuoteDTO>> GetPendingQuotes();
        Task<IEnumerable<QuoteDTO>> SearchQuotes(string searchText);
        Task<bool> ValidateQuotePayment(int quoteId, string paymentMethod);
        Task<bool> IsQuoteValidForConversion(int quoteId);
    }
}