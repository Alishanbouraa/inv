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
    public class ExpenseService : BaseService<Expense, ExpenseDTO>, IExpenseService
    {
        private readonly IDrawerService _drawerService;
        private readonly IUnitOfWork _unitOfWork;

        public ExpenseService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IDrawerService drawerService,
            IEventAggregator eventAggregator,
            IDbContextScopeService dbContextScopeService)
            : base(unitOfWork, mapper, eventAggregator, dbContextScopeService)
        {
            _drawerService = drawerService;
            _unitOfWork = unitOfWork;
        }

        public async Task<decimal> GetTotalExpensesAsync(DateTime startDate, DateTime endDate)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var expenses = await _repository.Query()
                    .Where(e => e.Date >= startDate && e.Date <= endDate)
                    .ToListAsync();

                return expenses.Sum(e => e.Amount);
            });
        }

        public async Task<IEnumerable<ExpenseDTO>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var expenses = await _repository.Query()
                    .Where(e => e.Date >= startDate && e.Date <= endDate)
                    .OrderByDescending(e => e.Date)
                    .ToListAsync();

                return _mapper.Map<IEnumerable<ExpenseDTO>>(expenses);
            });
        }

        public async Task CreateEmployeeSalaryExpenseAsync(int employeeId, decimal amount, string employeeName)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    var expense = new Expense
                    {
                        Reason = $"Salary withdrawal - {employeeName}",
                        Amount = amount,
                        Date = DateTime.Now,
                        Category = "Employee Salary",
                        Notes = $"Employee ID: {employeeId}",
                        CreatedAt = DateTime.Now
                    };

                    await _repository.AddAsync(expense);
                    await _unitOfWork.SaveChangesAsync();

                    // Process drawer transaction
                    await _drawerService.ProcessExpenseAsync(
                        amount,
                        "Employee Salary",
                        $"Salary withdrawal - {employeeName}"
                    );

                    await transaction.CommitAsync();

                    // Publish event for expense creation
                    _eventAggregator.Publish(new EntityChangedEvent<ExpenseDTO>(
                        "Create",
                        _mapper.Map<ExpenseDTO>(expense)
                    ));
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public override async Task<ExpenseDTO> CreateAsync(ExpenseDTO dto)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    // Validate expense data
                    if (string.IsNullOrWhiteSpace(dto.Reason))
                        throw new InvalidOperationException("Expense reason is required");

                    if (dto.Amount <= 0)
                        throw new InvalidOperationException("Amount must be greater than zero");

                    // Check drawer balance first
                    var drawer = await _drawerService.GetCurrentDrawerAsync();
                    if (drawer == null)
                        throw new InvalidOperationException("No active drawer found");

                    if (dto.Amount > drawer.CurrentBalance)
                        throw new InvalidOperationException("Insufficient funds in drawer");

                    // Create the expense record
                    var entity = _mapper.Map<Expense>(dto);
                    entity.CreatedAt = DateTime.Now;
                    var result = await _repository.AddAsync(entity);
                    await _unitOfWork.SaveChangesAsync();

                    // Process the drawer transaction
                    await _drawerService.ProcessExpenseAsync(
                        dto.Amount,
                        dto.Category,
                        dto.Reason
                    );

                    await transaction.CommitAsync();

                    // Map and return result
                    var resultDto = _mapper.Map<ExpenseDTO>(result);
                    _eventAggregator.Publish(new EntityChangedEvent<ExpenseDTO>("Create", resultDto));
                    _eventAggregator.Publish(new DrawerUpdateEvent(
                        "Expense",
                        -dto.Amount,
                        $"Expense: {dto.Category} - {dto.Reason}"
                    ));

                    return resultDto;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Debug.WriteLine($"Error creating expense: {ex.Message}");
                    throw;
                }
            });
        }

        public override async Task UpdateAsync(ExpenseDTO dto)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    var existingExpense = await _repository.GetByIdAsync(dto.ExpenseId);
                    if (existingExpense == null)
                    {
                        throw new InvalidOperationException($"Expense with ID {dto.ExpenseId} not found");
                    }

                    // Calculate difference in amount
                    decimal amountDifference = dto.Amount - existingExpense.Amount;

                    if (amountDifference > 0)
                    {
                        // If new amount is higher, check if we have enough in drawer
                        var drawerValid = await _drawerService.ValidateTransactionAsync(amountDifference, true);
                        if (!drawerValid)
                        {
                            throw new InvalidOperationException("Insufficient funds in cash drawer for expense increase");
                        }
                    }

                    // Update the expense record
                    _mapper.Map(dto, existingExpense);
                    await _repository.UpdateAsync(existingExpense);

                    // If amount changed, process drawer adjustment
                    if (amountDifference != 0)
                    {
                        await _drawerService.ProcessExpenseAsync(
                            amountDifference,
                            dto.Category,
                            $"Expense adjustment: {dto.Reason}"
                        );
                    }

                    await _unitOfWork.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _eventAggregator.Publish(new EntityChangedEvent<ExpenseDTO>("Update", dto));
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public override async Task DeleteAsync(int id)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    var expense = await _repository.GetByIdAsync(id);
                    if (expense == null)
                        throw new InvalidOperationException($"Expense with ID {id} not found");

                    // Refund the amount to the drawer
                    await _drawerService.AddCashTransactionAsync(expense.Amount, true);

                    await _repository.DeleteAsync(expense);
                    await _unitOfWork.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var expenseDto = _mapper.Map<ExpenseDTO>(expense);
                    _eventAggregator.Publish(new EntityChangedEvent<ExpenseDTO>("Delete", expenseDto));
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task<IEnumerable<ExpenseDTO>> GetByCategoryAsync(string category)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var expenses = await _repository.Query()
                    .Where(e => e.Category == category)
                    .OrderByDescending(e => e.Date)
                    .ToListAsync();

                return _mapper.Map<IEnumerable<ExpenseDTO>>(expenses);
            });
        }

        public async Task<Dictionary<string, decimal>> GetCategoryTotalsAsync(DateTime startDate, DateTime endDate)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                return await _repository.Query()
                    .Where(e => e.Date >= startDate && e.Date <= endDate)
                    .GroupBy(e => e.Category)
                    .ToDictionaryAsync(
                        g => g.Key,
                        g => g.Sum(e => e.Amount)
                    );
            });
        }

        public async Task<bool> ValidateExpenseAsync(decimal amount)
        {
            // Check if there's an active drawer with sufficient funds
            return await _drawerService.ValidateTransactionAsync(amount, true);
        }
    }
}