using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Interfaces;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.Domain.Entities;
using QuickTechSystems.Domain.Interfaces.Repositories;
using QuickTechSystems.Domain.Enums;
using System.Windows;
using System.IO;
using Microsoft.Win32;
using System.Collections.ObjectModel;

namespace QuickTechSystems.Application.Services
{
    public class QuoteService : BaseService<Quote, QuoteDTO>, IQuoteService
    {
        private readonly ITransactionService _transactionService;
        private readonly ICustomerService _customerService;
        private readonly IDrawerService _drawerService;
        private readonly IUnitOfWork _unitOfWork;

        public QuoteService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ITransactionService transactionService,
            ICustomerService customerService,
            IDrawerService drawerService,
            IEventAggregator eventAggregator,
            IDbContextScopeService dbContextScopeService)
            : base(unitOfWork, mapper, eventAggregator, dbContextScopeService)
        {
            _unitOfWork = unitOfWork;
            _transactionService = transactionService;
            _customerService = customerService;
            _drawerService = drawerService;
        }

        public async Task<IEnumerable<QuoteDTO>> SearchQuotes(string searchText)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var quotes = await _repository.Query()
                    .Where(q => q.QuoteNumber.Contains(searchText) ||
                                q.CustomerName.Contains(searchText))
                    .ToListAsync();
                return _mapper.Map<IEnumerable<QuoteDTO>>(quotes);
            });
        }

        public async Task<IEnumerable<QuoteDTO>> GetQuotesByCustomer(int customerId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var quotes = await _repository.Query()
                    .Where(q => q.CustomerId == customerId)
                    .ToListAsync();
                return _mapper.Map<IEnumerable<QuoteDTO>>(quotes);
            });
        }

        public async Task<IEnumerable<QuoteDTO>> GetPendingQuotes()
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var quotes = await _repository.Query()
                    .Where(q => q.Status == "Pending")
                    .ToListAsync();
                return _mapper.Map<IEnumerable<QuoteDTO>>(quotes);
            });
        }

        public async Task<QuoteDTO> CreateQuoteFromTransaction(TransactionDTO transaction)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var quote = new Quote
                {
                    CustomerId = transaction.CustomerId,
                    CustomerName = transaction.CustomerName,
                    TotalAmount = transaction.TotalAmount,
                    CreatedDate = DateTime.Now,
                    ExpiryDate = DateTime.Now.AddDays(30),
                    Status = "Pending",
                    QuoteNumber = $"Q-{DateTime.Now:yyyyMMddHHmmss}",
                    QuoteDetails = transaction.Details.Select(detail => new QuoteDetail
                    {
                        ProductId = detail.ProductId,
                        ProductName = detail.ProductName,
                        UnitPrice = detail.UnitPrice,
                        Quantity = detail.Quantity, // Now decimal to decimal - no conversion needed
                        Total = detail.Total
                    }).ToList()
                };

                await _repository.AddAsync(quote);
                await _unitOfWork.SaveChangesAsync();

                return _mapper.Map<QuoteDTO>(quote);
            });
        }

        public async Task<QuoteDTO> ConvertToTransaction(int quoteId, string paymentMethod)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    var quote = await _repository.Query()
                        .Include(q => q.QuoteDetails)
                        .FirstOrDefaultAsync(q => q.QuoteId == quoteId);

                    if (quote == null)
                        throw new InvalidOperationException("Quote not found");

                    // Only support Cash payment method
                    if (paymentMethod != "Cash")
                        throw new InvalidOperationException("Only Cash payment method is supported");

                    // Create transaction
                    var transactionDto = new TransactionDTO
                    {
                        CustomerId = quote.CustomerId,
                        CustomerName = quote.CustomerName,
                        TotalAmount = quote.TotalAmount,
                        PaidAmount = quote.TotalAmount, // Full payment with cash
                        TransactionDate = DateTime.Now,
                        TransactionType = TransactionType.Sale,
                        Status = TransactionStatus.Completed,
                        PaymentMethod = paymentMethod,
                        Details = new ObservableCollection<TransactionDetailDTO>(
                            quote.QuoteDetails.Select(qd => new TransactionDetailDTO
                            {
                                ProductId = qd.ProductId,
                                ProductName = qd.ProductName,
                                Quantity = qd.Quantity, // Now decimal to decimal - no conversion needed
                                UnitPrice = qd.UnitPrice,
                                Total = qd.Total
                            })
                        )
                    };

                    // Process the sale transaction
                    var processedTransaction = await _transactionService.ProcessSaleAsync(transactionDto);

                    // Process drawer update for cash payments
                    await _drawerService.ProcessCashSaleAsync(
                        quote.TotalAmount,
                        $"Quote conversion #{quote.QuoteNumber}"
                    );

                    // Update quote status
                    quote.Status = "Converted";
                    await _repository.UpdateAsync(quote);
                    await _unitOfWork.SaveChangesAsync();

                    await transaction.CommitAsync();

                    // Publish drawer update event after successful conversion
                    _eventAggregator.Publish(new DrawerUpdateEvent(
                        "Cash Sale",
                        quote.TotalAmount,
                        $"Quote conversion #{quote.QuoteNumber}"
                    ));

                    return _mapper.Map<QuoteDTO>(quote);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task<byte[]> GenerateQuotePdf(int quoteId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var quote = await _repository.Query()
                    .Include(q => q.QuoteDetails)
                    .FirstOrDefaultAsync(q => q.QuoteId == quoteId);

                if (quote == null)
                    throw new InvalidOperationException("Quote not found");

                using (var ms = new MemoryStream())
                {
                    using (var sw = new StreamWriter(ms))
                    {
                        sw.WriteLine("GalaxyNet");
                        sw.WriteLine("Your partner in all your IT problems");
                        sw.WriteLine("81 20 77 06");
                        sw.WriteLine("03 65 74 64");
                        sw.WriteLine("".PadRight(40, '-'));

                        sw.WriteLine($"Quote Number: {quote.QuoteNumber}");
                        sw.WriteLine($"Date: {quote.CreatedDate:dd/MM/yyyy}");
                        sw.WriteLine($"Customer: {quote.CustomerName}");
                        sw.WriteLine("".PadRight(40, '-'));

                        sw.WriteLine("{0,-20} {1,10} {2,10} {3,10}", "Product", "Quantity", "Unit Price", "Total");
                        sw.WriteLine("".PadRight(40, '-'));

                        foreach (var detail in quote.QuoteDetails)
                        {
                            sw.WriteLine("{0,-20} {1,10} {2,10:C2} {3,10:C2}",
                                detail.ProductName.Length > 20 ? detail.ProductName.Substring(0, 17) + "..." : detail.ProductName,
                                detail.Quantity,
                                detail.UnitPrice,
                                detail.Total);
                        }

                        sw.WriteLine("".PadRight(40, '-'));
                        sw.WriteLine("{0,40:C2}", quote.TotalAmount);

                        sw.Flush();
                    }
                    return ms.ToArray();
                }
            });
        }

        public override async Task<QuoteDTO> CreateAsync(QuoteDTO dto)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var entity = _mapper.Map<Quote>(dto);
                var result = await _repository.AddAsync(entity);
                await _unitOfWork.SaveChangesAsync();

                var resultDto = _mapper.Map<QuoteDTO>(result);
                _eventAggregator.Publish(new EntityChangedEvent<QuoteDTO>("Create", resultDto));

                return resultDto;
            });
        }

        public override async Task UpdateAsync(QuoteDTO dto)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var entity = await _repository.GetByIdAsync(dto.QuoteId);
                if (entity == null)
                    throw new InvalidOperationException($"Quote {dto.QuoteId} not found");

                _mapper.Map(dto, entity);
                await _repository.UpdateAsync(entity);
                await _unitOfWork.SaveChangesAsync();

                _eventAggregator.Publish(new EntityChangedEvent<QuoteDTO>("Update", dto));
            });
        }

        public override async Task DeleteAsync(int id)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null)
                    throw new InvalidOperationException($"Quote {id} not found");

                if (entity.Status == "Converted")
                    throw new InvalidOperationException("Cannot delete a converted quote");

                await _repository.DeleteAsync(entity);
                await _unitOfWork.SaveChangesAsync();

                var dto = _mapper.Map<QuoteDTO>(entity);
                _eventAggregator.Publish(new EntityChangedEvent<QuoteDTO>("Delete", dto));
            });
        }

        public async Task<bool> IsQuoteValidForConversion(int quoteId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var quote = await _repository.GetByIdAsync(quoteId);
                if (quote == null) return false;

                return quote.Status == "Pending" &&
                       quote.ExpiryDate >= DateTime.Now;
            });
        }

        public async Task<bool> ValidateQuotePayment(int quoteId, string paymentMethod)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var quote = await _repository.GetByIdAsync(quoteId);
                if (quote == null) return false;

                if (paymentMethod == "Cash")
                {
                    // Check if there's an active drawer
                    return await _drawerService.ValidateTransactionAsync(quote.TotalAmount, false);
                }

                return false; // Only cash payment method is supported
            });
        }
    }
}