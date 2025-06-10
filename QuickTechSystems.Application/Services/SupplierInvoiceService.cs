// Path: QuickTechSystems.Application.Services/SupplierInvoiceService.cs
using System.Diagnostics;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Interfaces;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.Domain.Entities;
using QuickTechSystems.Domain.Interfaces.Repositories;

namespace QuickTechSystems.Application.Services
{
    public class SupplierInvoiceService : ISupplierInvoiceService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDbContextScopeService _dbContextScopeService;
        private readonly IDrawerService _drawerService;

        public SupplierInvoiceService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IEventAggregator eventAggregator,
            IDbContextScopeService dbContextScopeService,
            IDrawerService drawerService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _eventAggregator = eventAggregator;
            _dbContextScopeService = dbContextScopeService;
            _drawerService = drawerService;
        }

        public async Task<IEnumerable<SupplierInvoiceDTO>> GetAllAsync()
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var invoices = await _unitOfWork.SupplierInvoices.Query()
                    .Include(si => si.Supplier)
                    .Include(si => si.Details)
                        .ThenInclude(d => d.Product)
                    .OrderByDescending(si => si.CreatedAt)
                    .ToListAsync();

                return _mapper.Map<IEnumerable<SupplierInvoiceDTO>>(invoices);
            });
        }

        public async Task<IEnumerable<SupplierInvoiceDTO>> GetBySupplierAsync(int supplierId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var invoices = await _unitOfWork.SupplierInvoices.Query()
                    .Include(si => si.Supplier)
                    .Include(si => si.Details)
                        .ThenInclude(d => d.Product)
                    .Where(si => si.SupplierId == supplierId)
                    .OrderByDescending(si => si.CreatedAt)
                    .ToListAsync();

                return _mapper.Map<IEnumerable<SupplierInvoiceDTO>>(invoices);
            });
        }
        public async Task<IEnumerable<SupplierInvoiceDTO>> GetRecentInvoicesAsync(DateTime startDate)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var invoices = await _unitOfWork.SupplierInvoices.Query()
                    .Include(si => si.Supplier)
                    .Include(si => si.Details)
                        .ThenInclude(d => d.Product)
                    .Where(si => si.CreatedAt >= startDate)
                    .OrderByDescending(si => si.CreatedAt)
                    .ToListAsync();

                var dtos = _mapper.Map<IEnumerable<SupplierInvoiceDTO>>(invoices);

                // Ensure supplier names are properly set
                foreach (var dto in dtos)
                {
                    var invoice = invoices.FirstOrDefault(i => i.SupplierInvoiceId == dto.SupplierInvoiceId);
                    if (invoice != null && invoice.Supplier != null)
                    {
                        dto.SupplierName = invoice.Supplier.Name;
                    }
                }

                return dtos;
            });
        }
        public async Task<SupplierInvoiceDTO?> GetByIdAsync(int invoiceId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var invoice = await _unitOfWork.SupplierInvoices.Query()
                    .Include(si => si.Supplier)
                    .Include(si => si.Details)
                        .ThenInclude(d => d.Product)
                    .FirstOrDefaultAsync(si => si.SupplierInvoiceId == invoiceId);

                return _mapper.Map<SupplierInvoiceDTO>(invoice);
            });
        }

        public async Task<SupplierInvoiceDTO> CreateAsync(SupplierInvoiceDTO invoiceDto)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var invoice = _mapper.Map<SupplierInvoice>(invoiceDto);
                invoice.CreatedAt = DateTime.Now;

                var result = await _unitOfWork.SupplierInvoices.AddAsync(invoice);
                await _unitOfWork.SaveChangesAsync();

                // Reload to get the supplier info
                result = await _unitOfWork.SupplierInvoices.Query()
                    .Include(si => si.Supplier)
                    .FirstOrDefaultAsync(si => si.SupplierInvoiceId == result.SupplierInvoiceId);

                var resultDto = _mapper.Map<SupplierInvoiceDTO>(result);

                // Publish event
                _eventAggregator.Publish(new EntityChangedEvent<SupplierInvoiceDTO>("Create", resultDto));

                return resultDto;
            });
        }

        public async Task UpdateAsync(SupplierInvoiceDTO invoiceDto)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var existingInvoice = await _unitOfWork.SupplierInvoices.GetByIdAsync(invoiceDto.SupplierInvoiceId);
                if (existingInvoice == null)
                {
                    throw new InvalidOperationException($"Invoice with ID {invoiceDto.SupplierInvoiceId} not found");
                }

                _mapper.Map(invoiceDto, existingInvoice);
                existingInvoice.UpdatedAt = DateTime.Now;

                await _unitOfWork.SupplierInvoices.UpdateAsync(existingInvoice);
                await _unitOfWork.SaveChangesAsync();

                // Publish event
                _eventAggregator.Publish(new EntityChangedEvent<SupplierInvoiceDTO>("Update", invoiceDto));
            });
        }

        public async Task DeleteAsync(int invoiceId)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var invoice = await _unitOfWork.SupplierInvoices.GetByIdAsync(invoiceId);
                if (invoice == null)
                {
                    throw new InvalidOperationException($"Invoice with ID {invoiceId} not found");
                }

                // Can only delete invoices in Draft status
                if (invoice.Status != "Draft")
                {
                    throw new InvalidOperationException($"Cannot delete invoice in {invoice.Status} status");
                }

                // Delete details first (should be handled by cascade delete, but just to be safe)
                var details = await _unitOfWork.SupplierInvoiceDetails.Query()
                    .Where(d => d.SupplierInvoiceId == invoiceId)
                    .ToListAsync();

                foreach (var detail in details)
                {
                    await _unitOfWork.SupplierInvoiceDetails.DeleteAsync(detail);
                }

                await _unitOfWork.SupplierInvoices.DeleteAsync(invoice);
                await _unitOfWork.SaveChangesAsync();

                // Publish event
                var invoiceDto = _mapper.Map<SupplierInvoiceDTO>(invoice);
                _eventAggregator.Publish(new EntityChangedEvent<SupplierInvoiceDTO>("Delete", invoiceDto));
            });
        }

        public async Task<IEnumerable<SupplierInvoiceDTO>> GetByStatusAsync(string status)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var invoices = await _unitOfWork.SupplierInvoices.Query()
                    .Include(si => si.Supplier)
                    .Include(si => si.Details)
                        .ThenInclude(d => d.Product)
                    .Where(si => si.Status == status)
                    .OrderByDescending(si => si.CreatedAt)
                    .ToListAsync();

                return _mapper.Map<IEnumerable<SupplierInvoiceDTO>>(invoices);
            });
        }

        public async Task<bool> ValidateInvoiceAsync(int invoiceId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var invoice = await _unitOfWork.SupplierInvoices.GetByIdAsync(invoiceId);
                if (invoice == null)
                {
                    throw new InvalidOperationException($"Invoice with ID {invoiceId} not found");
                }

                // Only draft invoices can be validated
                if (invoice.Status != "Draft")
                {
                    throw new InvalidOperationException($"Cannot validate invoice in {invoice.Status} status");
                }

                // Check if there are any details
                var details = await _unitOfWork.SupplierInvoiceDetails.Query()
                    .Where(d => d.SupplierInvoiceId == invoiceId)
                    .ToListAsync();

                if (!details.Any())
                {
                    throw new InvalidOperationException("Cannot validate invoice with no product details");
                }

                // Update status to Validated
                invoice.Status = "Validated";
                invoice.UpdatedAt = DateTime.Now;

                await _unitOfWork.SupplierInvoices.UpdateAsync(invoice);
                await _unitOfWork.SaveChangesAsync();

                // Publish event
                var invoiceDto = _mapper.Map<SupplierInvoiceDTO>(invoice);
                _eventAggregator.Publish(new EntityChangedEvent<SupplierInvoiceDTO>("Update", invoiceDto));

                return true;
            });
        }

        public async Task<bool> SettleInvoiceAsync(int invoiceId, decimal paymentAmount = 0)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    var invoice = await _unitOfWork.SupplierInvoices.Query()
                        .Include(si => si.Supplier)
                        .FirstOrDefaultAsync(si => si.SupplierInvoiceId == invoiceId);

                    if (invoice == null)
                    {
                        throw new InvalidOperationException($"Invoice with ID {invoiceId} not found");
                    }

                    // Only validated invoices can be settled
                    if (invoice.Status != "Validated")
                    {
                        throw new InvalidOperationException($"Cannot settle invoice in {invoice.Status} status");
                    }

                    // Get existing payments for this invoice
                    var existingPayments = await _unitOfWork.Context.Set<SupplierTransaction>()
                        .Where(st => st.Reference != null && st.Reference.Contains($"INV-{invoice.InvoiceNumber}"))
                        .SumAsync(st => Math.Abs(st.Amount));

                    // Calculate remaining amount
                    decimal remainingAmount = invoice.TotalAmount - existingPayments;

                    // If no payment amount specified, use the full remaining amount
                    if (paymentAmount <= 0)
                    {
                        paymentAmount = remainingAmount;
                    }

                    // Validate payment amount
                    if (paymentAmount > remainingAmount)
                    {
                        throw new InvalidOperationException($"Payment amount ({paymentAmount:C}) exceeds remaining amount ({remainingAmount:C})");
                    }

                    // Verify drawer has enough funds
                    var drawer = await _drawerService.GetCurrentDrawerAsync();
                    if (drawer == null)
                    {
                        throw new InvalidOperationException("No active cash drawer. Please open a drawer first.");
                    }

                    if (drawer.CurrentBalance < paymentAmount)
                    {
                        throw new InvalidOperationException("Insufficient funds in drawer to settle this invoice.");
                    }

                    // Create a supplier transaction to track the payment
                    string reference = $"INV-{invoice.InvoiceNumber}";
                    var supplierTransaction = new SupplierTransaction
                    {
                        SupplierId = invoice.SupplierId,
                        Amount = paymentAmount, // Positive amount for purchases
                        TransactionType = "Purchase",
                        Reference = reference,
                        Notes = $"Payment for invoice {invoice.InvoiceNumber}",
                        TransactionDate = DateTime.Now
                    };

                    await _unitOfWork.Context.Set<SupplierTransaction>().AddAsync(supplierTransaction);

                    // Update supplier balance
                    var supplier = await _unitOfWork.Suppliers.GetByIdAsync(invoice.SupplierId);
                    if (supplier != null)
                    {
                        supplier.Balance += paymentAmount;
                        supplier.UpdatedAt = DateTime.Now;
                        await _unitOfWork.Suppliers.UpdateAsync(supplier);
                    }

                    // Process the drawer transaction
                    await _drawerService.ProcessSupplierInvoiceAsync(
                        paymentAmount,
                        invoice.Supplier?.Name ?? "Unknown Supplier",
                        reference
                    );

                    // Only mark as settled if fully paid
                    if (existingPayments + paymentAmount >= invoice.TotalAmount - 0.01m) // Using epsilon for decimal comparison
                    {
                        invoice.Status = "Settled";
                        invoice.UpdatedAt = DateTime.Now;

                        await _unitOfWork.SupplierInvoices.UpdateAsync(invoice);
                    }

                    await _unitOfWork.SaveChangesAsync();

                    await transaction.CommitAsync();

                    // Publish events
                    var invoiceDto = _mapper.Map<SupplierInvoiceDTO>(invoice);
                    _eventAggregator.Publish(new EntityChangedEvent<SupplierInvoiceDTO>("Update", invoiceDto));

                    var supplierDto = _mapper.Map<SupplierDTO>(supplier);
                    _eventAggregator.Publish(new EntityChangedEvent<SupplierDTO>("Update", supplierDto));

                    _eventAggregator.Publish(new DrawerUpdateEvent(
                        "Supplier Invoice",
                        -paymentAmount,
                        $"Payment for invoice {invoice.InvoiceNumber} for {invoice.Supplier?.Name ?? "Unknown Supplier"}"
                    ));

                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Debug.WriteLine($"Error settling invoice: {ex.Message}");
                    throw;
                }
            });
        }

        public async Task<IEnumerable<SupplierTransactionDTO>> GetInvoicePaymentsAsync(int invoiceId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var invoice = await _unitOfWork.SupplierInvoices.GetByIdAsync(invoiceId);
                if (invoice == null)
                {
                    throw new InvalidOperationException($"Invoice with ID {invoiceId} not found");
                }

                var payments = await _unitOfWork.Context.Set<SupplierTransaction>()
                    .Include(st => st.Supplier)
                    .Where(st => st.Reference != null && st.Reference.Contains($"INV-{invoice.InvoiceNumber}"))
                    .OrderByDescending(st => st.TransactionDate)
                    .ToListAsync();

                return _mapper.Map<IEnumerable<SupplierTransactionDTO>>(payments);
            });
        }
        public async Task<IEnumerable<string>> GetInvoiceNumbersForAutocompleteAsync(string searchTerm)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                return await _unitOfWork.SupplierInvoices.Query()
                    .Where(si => si.Status == "Draft" && si.InvoiceNumber.Contains(searchTerm))
                    .Select(si => si.InvoiceNumber)
                    .Take(10)
                    .ToListAsync();
            });
        }

        public async Task<SupplierInvoiceDTO?> GetByInvoiceNumberAsync(string invoiceNumber, int supplierId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var invoice = await _unitOfWork.SupplierInvoices.Query()
                    .Include(si => si.Supplier)
                    .Include(si => si.Details)
                        .ThenInclude(d => d.Product)
                    .FirstOrDefaultAsync(si => si.InvoiceNumber == invoiceNumber && si.SupplierId == supplierId);

                return _mapper.Map<SupplierInvoiceDTO>(invoice);
            });
        }

        public async Task UpdateCalculatedAmountAsync(int invoiceId)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                // Get all the details for this invoice
                var details = await _unitOfWork.SupplierInvoiceDetails.Query()
                    .Where(d => d.SupplierInvoiceId == invoiceId)
                    .ToListAsync();

                // Calculate the sum of all product total prices
                decimal calculatedAmount = details.Sum(d => d.TotalPrice);

                // Update the invoice's calculated amount
                var invoice = await _unitOfWork.SupplierInvoices.GetByIdAsync(invoiceId);
                if (invoice != null)
                {
                    invoice.CalculatedAmount = calculatedAmount;
                    invoice.UpdatedAt = DateTime.Now;

                    await _unitOfWork.SupplierInvoices.UpdateAsync(invoice);
                    await _unitOfWork.SaveChangesAsync();

                    // Publish event
                    var invoiceDto = _mapper.Map<SupplierInvoiceDTO>(invoice);
                    _eventAggregator.Publish(new EntityChangedEvent<SupplierInvoiceDTO>("Update", invoiceDto));
                }
            });
        }

        // Path: QuickTechSystems.Application.Services/SupplierInvoiceService.cs

        // Update the AddProductToInvoiceAsync method to use direct SQL
        public async Task AddProductToInvoiceAsync(SupplierInvoiceDetailDTO detailDto)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    // First check if the invoice exists and is in draft status
                    var invoiceStatus = await _unitOfWork.Context.Set<SupplierInvoice>()
                        .Where(i => i.SupplierInvoiceId == detailDto.SupplierInvoiceId)
                        .Select(i => new { i.Status })
                        .FirstOrDefaultAsync();

                    if (invoiceStatus == null)
                    {
                        throw new InvalidOperationException($"Invoice with ID {detailDto.SupplierInvoiceId} not found");
                    }

                    if (invoiceStatus.Status != "Draft")
                    {
                        throw new InvalidOperationException($"Cannot add products to invoice in {invoiceStatus.Status} status");
                    }

                    // Verify product exists without tracking
                    bool productExists = await _unitOfWork.Context.Set<Product>()
                        .AsNoTracking()
                        .AnyAsync(p => p.ProductId == detailDto.ProductId);

                    if (!productExists)
                    {
                        throw new InvalidOperationException($"Product with ID {detailDto.ProductId} not found");
                    }

                    // Calculate total price
                    detailDto.TotalPrice = detailDto.Quantity * detailDto.PurchasePrice;

                    // Use direct SQL to add the invoice detail, avoiding entity tracking issues
                    string sql = @"
                INSERT INTO SupplierInvoiceDetails (
                    SupplierInvoiceId, ProductId, Quantity, PurchasePrice, TotalPrice, 
                    BoxBarcode, NumberOfBoxes, ItemsPerBox, BoxPurchasePrice, BoxSalePrice
                ) VALUES (
                    @supplierId, @productId, @quantity, @purchasePrice, @totalPrice,
                    @boxBarcode, @numberOfBoxes, @itemsPerBox, @boxPurchasePrice, @boxSalePrice
                )";

                    var parameters = new[]
                    {
                new Microsoft.Data.SqlClient.SqlParameter("@supplierId", detailDto.SupplierInvoiceId),
                new Microsoft.Data.SqlClient.SqlParameter("@productId", detailDto.ProductId),
                new Microsoft.Data.SqlClient.SqlParameter("@quantity", detailDto.Quantity),
                new Microsoft.Data.SqlClient.SqlParameter("@purchasePrice", detailDto.PurchasePrice),
                new Microsoft.Data.SqlClient.SqlParameter("@totalPrice", detailDto.TotalPrice),
                new Microsoft.Data.SqlClient.SqlParameter("@boxBarcode", (object)detailDto.BoxBarcode ?? DBNull.Value),
                new Microsoft.Data.SqlClient.SqlParameter("@numberOfBoxes", detailDto.NumberOfBoxes),
                new Microsoft.Data.SqlClient.SqlParameter("@itemsPerBox", detailDto.ItemsPerBox),
                new Microsoft.Data.SqlClient.SqlParameter("@boxPurchasePrice", detailDto.BoxPurchasePrice),
                new Microsoft.Data.SqlClient.SqlParameter("@boxSalePrice", detailDto.BoxSalePrice)
            };

                    await _unitOfWork.Context.Database.ExecuteSqlRawAsync(sql, parameters);

                    // Update the calculated amount on the invoice
                    await UpdateCalculatedAmountAsync(detailDto.SupplierInvoiceId);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error adding product to invoice: {ex}");
                    throw;
                }
            });
        }
 
        public async Task RemoveProductFromInvoiceAsync(int detailId)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    // Get the detail
                    var detail = await _unitOfWork.SupplierInvoiceDetails.GetByIdAsync(detailId);
                    if (detail == null)
                    {
                        throw new InvalidOperationException($"Invoice detail with ID {detailId} not found");
                    }

                    // Check if the invoice is in draft status
                    var invoice = await _unitOfWork.SupplierInvoices.GetByIdAsync(detail.SupplierInvoiceId);
                    if (invoice == null)
                    {
                        throw new InvalidOperationException($"Invoice not found");
                    }

                    if (invoice.Status != "Draft")
                    {
                        throw new InvalidOperationException($"Cannot remove products from invoice in {invoice.Status} status");
                    }

                    // Get the product to update inventory
                    var product = await _unitOfWork.Products.GetByIdAsync(detail.ProductId);
                    if (product != null)
                    {
                        // Decrease inventory
                        product.CurrentStock -= (int)detail.Quantity;
                        product.UpdatedAt = DateTime.Now;
                        await _unitOfWork.Products.UpdateAsync(product);

                        // Add inventory history record
                        var inventoryHistory = new InventoryHistory
                        {
                            ProductId = product.ProductId,
                            QuantityChange = -detail.Quantity,
                            NewQuantity = product.CurrentStock,
                            Type = "Adjustment",
                            Notes = $"Removed from supplier invoice {invoice.InvoiceNumber}",
                            Timestamp = DateTime.Now
                        };

                        await _unitOfWork.Context.Set<InventoryHistory>().AddAsync(inventoryHistory);
                    }

                    // Remove the detail
                    await _unitOfWork.SupplierInvoiceDetails.DeleteAsync(detail);
                    await _unitOfWork.SaveChangesAsync();

                    // Update the calculated amount on the invoice
                    await UpdateCalculatedAmountAsync(detail.SupplierInvoiceId);

                    await transaction.CommitAsync();

                    // Publish product update event if product exists
                    if (product != null)
                    {
                        var productDto = _mapper.Map<ProductDTO>(product);
                        _eventAggregator.Publish(new EntityChangedEvent<ProductDTO>("Update", productDto));
                    }
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Debug.WriteLine($"Error removing product from invoice: {ex.Message}");
                    throw;
                }
            });
        }
    }
}