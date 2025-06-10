using System.Diagnostics;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Interfaces;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.Domain.Entities;
using QuickTechSystems.Domain.Interfaces.Repositories;

namespace QuickTechSystems.Application.Services
{
    public class SupplierService : BaseService<Supplier, SupplierDTO>, ISupplierService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDrawerService _drawerService;

        public SupplierService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IDrawerService drawerService,
            IEventAggregator eventAggregator,
            IDbContextScopeService dbContextScopeService)
            : base(unitOfWork, mapper, eventAggregator, dbContextScopeService)
        {
            _unitOfWork = unitOfWork;
            _drawerService = drawerService;
        }

        public async Task<IEnumerable<SupplierDTO>> GetByNameAsync(string name)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var suppliers = await _repository.Query()
                    .Where(s => s.Name.Contains(name))
                    .ToListAsync();
                return _mapper.Map<IEnumerable<SupplierDTO>>(suppliers);
            });
        }
        public async Task<IEnumerable<SupplierDTO>> GetActiveAsync()
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var suppliers = await _repository.Query()
                    .Where(s => s.IsActive)
                    .ToListAsync();
                return _mapper.Map<IEnumerable<SupplierDTO>>(suppliers);
            });
        }

        public override async Task UpdateAsync(SupplierDTO dto)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    // Get the existing entity from the context
                    var existingSupplier = await _repository.GetByIdAsync(dto.SupplierId);
                    if (existingSupplier == null)
                    {
                        throw new InvalidOperationException($"Supplier with ID {dto.SupplierId} not found");
                    }

                    // Update the existing entity properties
                    _mapper.Map(dto, existingSupplier);

                    // Explicitly set the IsActive property to ensure it's tracked
                    existingSupplier.IsActive = dto.IsActive;

                    // Update the timestamp
                    existingSupplier.UpdatedAt = DateTime.Now;

                    await _repository.UpdateAsync(existingSupplier);
                    await _unitOfWork.SaveChangesAsync();

                    // Publish the update event
                    _eventAggregator.Publish(new EntityChangedEvent<SupplierDTO>("Update", dto));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error updating supplier: {ex}");
                    throw;
                }
            });
        }
        public async Task<bool> UpdateBalanceAsync(int supplierId, decimal amount, IDbContextTransaction? existingTransaction = null)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var shouldManageTransaction = existingTransaction == null;
                var transaction = existingTransaction ?? await _unitOfWork.BeginTransactionAsync();

                try
                {
                    var supplier = await _repository.GetByIdAsync(supplierId);
                    if (supplier == null) return false;

                    supplier.Balance += amount;
                    supplier.UpdatedAt = DateTime.Now;
                    await _repository.UpdateAsync(supplier);
                    await _unitOfWork.SaveChangesAsync();

                    if (shouldManageTransaction)
                    {
                        await transaction.CommitAsync();
                    }
                    return true;
                }
                catch
                {
                    if (shouldManageTransaction)
                    {
                        await transaction.RollbackAsync();
                    }
                    throw;
                }
            });
        }

        public async Task<IEnumerable<SupplierDTO>> GetWithOutstandingBalanceAsync()
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var suppliers = await _repository.Query()
                    .Where(s => s.Balance > 0)
                    .ToListAsync();
                return _mapper.Map<IEnumerable<SupplierDTO>>(suppliers);
            });
        }

        public async Task<IEnumerable<SupplierTransactionDTO>> GetSupplierTransactionsAsync(int supplierId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var transactions = await _unitOfWork.Context.Set<SupplierTransaction>()
                    .Include(st => st.Supplier)
                    .Where(st => st.SupplierId == supplierId)
                    .OrderByDescending(st => st.TransactionDate)
                    .ToListAsync();
                return _mapper.Map<IEnumerable<SupplierTransactionDTO>>(transactions);
            });
        }

        // File: QuickTechSystems.Application\Services\SupplierService.cs
        public async Task<SupplierTransactionDTO> AddTransactionAsync(SupplierTransactionDTO transactionDto, bool updateDrawer = true)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    if (updateDrawer)
                    {
                        var drawerValid = await _drawerService.ValidateTransactionAsync(Math.Abs(transactionDto.Amount), true);
                        if (!drawerValid)
                        {
                            throw new InvalidOperationException("Insufficient funds in cash drawer");
                        }
                    }

                    // Validate the supplier exists
                    var supplier = await _repository.GetByIdAsync(transactionDto.SupplierId);
                    if (supplier == null)
                    {
                        throw new InvalidOperationException($"Supplier with ID {transactionDto.SupplierId} not found");
                    }

                    var supplierTransaction = _mapper.Map<SupplierTransaction>(transactionDto);
                    await _unitOfWork.Context.Set<SupplierTransaction>().AddAsync(supplierTransaction);

                    // Update supplier balance
                    bool balanceUpdated = await UpdateBalanceAsync(transactionDto.SupplierId, transactionDto.Amount, transaction);
                    if (!balanceUpdated)
                    {
                        throw new InvalidOperationException($"Failed to update supplier balance for ID {transactionDto.SupplierId}");
                    }

                    if (updateDrawer)
                    {
                        await _drawerService.ProcessSupplierPaymentAsync(
                            Math.Abs(transactionDto.Amount),
                            transactionDto.SupplierName,
                            transactionDto.Reference ?? transactionDto.SupplierTransactionId.ToString()
                        );

                        // Publish drawer update event
                        _eventAggregator.Publish(new DrawerUpdateEvent(
                            "Supplier Payment",
                            -Math.Abs(transactionDto.Amount),
                            $"Payment to supplier - {transactionDto.SupplierName}"
                        ));
                    }

                    await _unitOfWork.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Publish supplier update event
                    _eventAggregator.Publish(new EntityChangedEvent<SupplierDTO>(
                        "Update",
                        _mapper.Map<SupplierDTO>(supplier)
                    ));

                    return _mapper.Map<SupplierTransactionDTO>(supplierTransaction);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw new ApplicationException($"Failed to process supplier payment: {ex.Message}", ex);
                }
            });
        }
    }
}