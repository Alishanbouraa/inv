// Path: QuickTechSystems.Application.Services/InventoryTransferService.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
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
    public class InventoryTransferService : IInventoryTransferService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDbContextScopeService _dbContextScopeService;

        public InventoryTransferService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IEventAggregator eventAggregator,
            IDbContextScopeService dbContextScopeService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _eventAggregator = eventAggregator;
            _dbContextScopeService = dbContextScopeService;
        }

        public async Task<IEnumerable<InventoryTransferDTO>> GetAllAsync()
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var transfers = await _unitOfWork.InventoryTransfers.Query()
                    .Include(t => t.MainStock)
                    .Include(t => t.Product)
                    .OrderByDescending(t => t.TransferDate)
                    .ToListAsync();

                return _mapper.Map<IEnumerable<InventoryTransferDTO>>(transfers);
            });
        }

        public async Task<IEnumerable<InventoryTransferDTO>> GetByMainStockIdAsync(int mainStockId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var transfers = await _unitOfWork.InventoryTransfers.Query()
                    .Include(t => t.MainStock)
                    .Include(t => t.Product)
                    .Where(t => t.MainStockId == mainStockId)
                    .OrderByDescending(t => t.TransferDate)
                    .ToListAsync();

                return _mapper.Map<IEnumerable<InventoryTransferDTO>>(transfers);
            });
        }

        public async Task<IEnumerable<InventoryTransferDTO>> GetByProductIdAsync(int productId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var transfers = await _unitOfWork.InventoryTransfers.Query()
                    .Include(t => t.MainStock)
                    .Include(t => t.Product)
                    .Where(t => t.ProductId == productId)
                    .OrderByDescending(t => t.TransferDate)
                    .ToListAsync();

                return _mapper.Map<IEnumerable<InventoryTransferDTO>>(transfers);
            });
        }

        public async Task<IEnumerable<InventoryTransferDTO>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var transfers = await _unitOfWork.InventoryTransfers.Query()
                    .Include(t => t.MainStock)
                    .Include(t => t.Product)
                    .Where(t => t.TransferDate >= startDate && t.TransferDate <= endDate)
                    .OrderByDescending(t => t.TransferDate)
                    .ToListAsync();

                return _mapper.Map<IEnumerable<InventoryTransferDTO>>(transfers);
            });
        }

        public async Task<InventoryTransferDTO> GetByIdAsync(int id)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var transfer = await _unitOfWork.InventoryTransfers.Query()
                    .Include(t => t.MainStock)
                    .Include(t => t.Product)
                    .FirstOrDefaultAsync(t => t.InventoryTransferId == id);

                return _mapper.Map<InventoryTransferDTO>(transfer);
            });
        }

        public async Task<InventoryTransferDTO> CreateAsync(InventoryTransferDTO transferDto)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    // Validate MainStock and Product existence
                    var mainStock = await _unitOfWork.MainStocks.GetByIdAsync(transferDto.MainStockId);
                    if (mainStock == null)
                    {
                        throw new InvalidOperationException($"MainStock with id {transferDto.MainStockId} not found");
                    }

                    var product = await _unitOfWork.Products.GetByIdAsync(transferDto.ProductId);
                    if (product == null)
                    {
                        throw new InvalidOperationException($"Product with id {transferDto.ProductId} not found");
                    }

                    // Check if we have enough stock to transfer
                    if (mainStock.CurrentStock < transferDto.Quantity)
                    {
                        throw new InvalidOperationException($"Insufficient stock in MainStock: {mainStock.CurrentStock} < {transferDto.Quantity}");
                    }

                    // Create the transfer
                    var transfer = _mapper.Map<InventoryTransfer>(transferDto);

                    // Generate a reference number if not provided
                    if (string.IsNullOrEmpty(transfer.ReferenceNumber))
                    {
                        transfer.ReferenceNumber = $"TRF-{DateTime.Now:yyyyMMddHHmmss}";
                    }

                    // Set transfer date if not set
                    if (transfer.TransferDate == default)
                    {
                        transfer.TransferDate = DateTime.Now;
                    }

                    // Add transfer record
                    await _unitOfWork.InventoryTransfers.AddAsync(transfer);

                    // Update MainStock quantity (deduct)
                    mainStock.CurrentStock -= transferDto.Quantity;
                    mainStock.UpdatedAt = DateTime.Now;
                    await _unitOfWork.MainStocks.UpdateAsync(mainStock);

                    // Update Product quantity (add)
                    product.CurrentStock += transferDto.Quantity;
                    product.UpdatedAt = DateTime.Now;
                    await _unitOfWork.Products.UpdateAsync(product);

                    // Add inventory history for MainStock
                    var mainStockHistory = new InventoryHistory
                    {
                        ProductId = transferDto.MainStockId, // This should be adjusted if needed to reference MainStock
                        QuantityChange = -transferDto.Quantity,
                        NewQuantity = mainStock.CurrentStock,
                        Type = "Transfer-Out",
                        Notes = $"Transfer to store inventory: {transfer.ReferenceNumber}",
                        Timestamp = DateTime.Now
                    };

                    await _unitOfWork.InventoryHistories.AddAsync(mainStockHistory);

                    // Add inventory history for Product
                    var productHistory = new InventoryHistory
                    {
                        ProductId = transferDto.ProductId,
                        QuantityChange = transferDto.Quantity,
                        NewQuantity = product.CurrentStock,
                        Type = "Transfer-In",
                        Notes = $"Transfer from main stock: {transfer.ReferenceNumber}",
                        Timestamp = DateTime.Now
                    };

                    await _unitOfWork.InventoryHistories.AddAsync(productHistory);

                    // Save all changes
                    await _unitOfWork.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Publish update events
                    var mainStockDto = _mapper.Map<MainStockDTO>(mainStock);
                    _eventAggregator.Publish(new EntityChangedEvent<MainStockDTO>("Update", mainStockDto));

                    var productDto = _mapper.Map<ProductDTO>(product);
                    _eventAggregator.Publish(new EntityChangedEvent<ProductDTO>("Update", productDto));

                    return _mapper.Map<InventoryTransferDTO>(transfer);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Debug.WriteLine($"Error creating inventory transfer: {ex.Message}");
                    throw;
                }
            });
        }

        public async Task<bool> CreateBulkTransferAsync(List<(int MainStockId, int ProductId, decimal Quantity)> transfers, string transferredBy, string? notes = null)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    string referenceNumber = $"BULK-TRF-{DateTime.Now:yyyyMMddHHmmss}";
                    var transferDate = DateTime.Now;

                    // Process each transfer
                    foreach (var transferItem in transfers)
                    {
                        // Validate existence and stock
                        var mainStock = await _unitOfWork.MainStocks.GetByIdAsync(transferItem.MainStockId);
                        if (mainStock == null)
                        {
                            throw new InvalidOperationException($"MainStock with id {transferItem.MainStockId} not found");
                        }

                        var product = await _unitOfWork.Products.GetByIdAsync(transferItem.ProductId);
                        if (product == null)
                        {
                            throw new InvalidOperationException($"Product with id {transferItem.ProductId} not found");
                        }

                        if (mainStock.CurrentStock < transferItem.Quantity)
                        {
                            throw new InvalidOperationException($"Insufficient stock in MainStock {mainStock.Name}: {mainStock.CurrentStock} < {transferItem.Quantity}");
                        }

                        // Create transfer record
                        var transfer = new InventoryTransfer
                        {
                            MainStockId = transferItem.MainStockId,
                            ProductId = transferItem.ProductId,
                            Quantity = transferItem.Quantity,
                            TransferDate = transferDate,
                            Notes = notes,
                            ReferenceNumber = $"{referenceNumber}-{transferItem.MainStockId}-{transferItem.ProductId}",
                            TransferredBy = transferredBy
                        };

                        await _unitOfWork.InventoryTransfers.AddAsync(transfer);

                        // Update MainStock quantity
                        mainStock.CurrentStock -= transferItem.Quantity;
                        mainStock.UpdatedAt = DateTime.Now;
                        await _unitOfWork.MainStocks.UpdateAsync(mainStock);

                        // Update Product quantity
                        product.CurrentStock += transferItem.Quantity;
                        product.UpdatedAt = DateTime.Now;
                        await _unitOfWork.Products.UpdateAsync(product);

                        // Add history records
                        var mainStockHistory = new InventoryHistory
                        {
                            ProductId = transferItem.MainStockId,
                            QuantityChange = -transferItem.Quantity,
                            NewQuantity = mainStock.CurrentStock,
                            Type = "Bulk-Transfer-Out",
                            Notes = $"Bulk transfer to store inventory: {referenceNumber}",
                            Timestamp = DateTime.Now
                        };

                        await _unitOfWork.InventoryHistories.AddAsync(mainStockHistory);

                        var productHistory = new InventoryHistory
                        {
                            ProductId = transferItem.ProductId,
                            QuantityChange = transferItem.Quantity,
                            NewQuantity = product.CurrentStock,
                            Type = "Bulk-Transfer-In",
                            Notes = $"Bulk transfer from main stock: {referenceNumber}",
                            Timestamp = DateTime.Now
                        };

                        await _unitOfWork.InventoryHistories.AddAsync(productHistory);

                        // Publish update events after each transfer
                        var mainStockDto = _mapper.Map<MainStockDTO>(mainStock);
                        _eventAggregator.Publish(new EntityChangedEvent<MainStockDTO>("Update", mainStockDto));

                        var productDto = _mapper.Map<ProductDTO>(product);
                        _eventAggregator.Publish(new EntityChangedEvent<ProductDTO>("Update", productDto));
                    }

                    // Save all changes
                    await _unitOfWork.SaveChangesAsync();
                    await transaction.CommitAsync();

                    Debug.WriteLine($"Bulk transfer completed successfully: {transfers.Count} items transferred");
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Debug.WriteLine($"Error during bulk transfer: {ex.Message}");
                    throw;
                }
            });
        }
    }
}