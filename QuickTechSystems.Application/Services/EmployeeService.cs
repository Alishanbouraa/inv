using System.Diagnostics;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Interfaces;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.Domain.Entities;
using QuickTechSystems.Domain.Interfaces.Repositories;
using System.Security.Cryptography;
using System.Text;

namespace QuickTechSystems.Application.Services
{
    public class EmployeeService : BaseService<Employee, EmployeeDTO>, IEmployeeService
    {
        private readonly IDrawerService _drawerService;
        private readonly IExpenseService _expenseService;

        public EmployeeService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IDrawerService drawerService,
            IExpenseService expenseService,
            IEventAggregator eventAggregator,
            IDbContextScopeService dbContextScopeService)
            : base(unitOfWork, mapper, eventAggregator, dbContextScopeService)
        {
            _drawerService = drawerService;
            _expenseService = expenseService;
        }

        public async Task<bool> ProcessSalaryWithdrawalAsync(int employeeId, decimal amount, string notes)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    var employee = await _repository.GetByIdAsync(employeeId);
                    if (employee == null || amount > employee.CurrentBalance)
                        return false;

                    // Create expense record
                    await _expenseService.CreateEmployeeSalaryExpenseAsync(
                        employeeId,
                        amount,
                        $"{employee.FirstName} {employee.LastName}"
                    );

                    // Update employee balance
                    employee.CurrentBalance -= amount;

                    var salaryTransaction = new EmployeeSalaryTransaction
                    {
                        EmployeeId = employeeId,
                        Amount = -amount,
                        TransactionType = "Withdrawal",
                        TransactionDate = DateTime.Now,
                        Notes = notes,
                        Employee = employee
                    };

                    await _unitOfWork.Context.Set<EmployeeSalaryTransaction>().AddAsync(salaryTransaction);
                    await _repository.UpdateAsync(employee);
                    await _unitOfWork.SaveChangesAsync();

                    _eventAggregator.Publish(new EntityChangedEvent<EmployeeDTO>(
                        "Update",
                        _mapper.Map<EmployeeDTO>(employee)
                    ));

                    await transaction.CommitAsync();
                    return true;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task ProcessMonthlySalaryAsync(int employeeId)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    var employee = await _repository.GetByIdAsync(employeeId);
                    if (employee == null)
                        throw new InvalidOperationException("Employee not found");

                    employee.CurrentBalance += employee.MonthlySalary;

                    var salaryTransaction = new EmployeeSalaryTransaction
                    {
                        EmployeeId = employeeId,
                        Amount = employee.MonthlySalary,
                        TransactionType = "Salary",
                        TransactionDate = DateTime.Now,
                        Notes = "Monthly salary payment",
                        Employee = employee
                    };

                    await _unitOfWork.Context.Set<EmployeeSalaryTransaction>().AddAsync(salaryTransaction);
                    await _repository.UpdateAsync(employee);
                    await _unitOfWork.SaveChangesAsync();

                    _eventAggregator.Publish(new EntityChangedEvent<EmployeeDTO>(
                        "Update",
                        _mapper.Map<EmployeeDTO>(employee)
                    ));

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task<IEnumerable<EmployeeSalaryTransactionDTO>> GetSalaryHistoryAsync(int employeeId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var transactions = await _unitOfWork.Context.Set<EmployeeSalaryTransaction>()
                    .Include(t => t.Employee)
                    .Where(t => t.EmployeeId == employeeId)
                    .OrderByDescending(t => t.TransactionDate)
                    .ToListAsync();

                return _mapper.Map<IEnumerable<EmployeeSalaryTransactionDTO>>(transactions);
            });
        }

        public async Task UpdateAsync(int employeeId, EmployeeDTO dto)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var existingEmployee = await _repository.GetByIdAsync(employeeId);
                if (existingEmployee == null)
                    throw new InvalidOperationException($"Employee with ID {employeeId} not found");

                _mapper.Map(dto, existingEmployee);
                await _repository.UpdateAsync(existingEmployee);
                await _unitOfWork.SaveChangesAsync();
            });
        }

        public async Task<EmployeeDTO?> GetByUsernameAsync(string username)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var employee = await _repository.Query()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Username == username);
                return _mapper.Map<EmployeeDTO>(employee);
            });
        }

        public async Task ResetPasswordAsync(int employeeId, string newPassword)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var employee = await _repository.GetByIdAsync(employeeId);
                if (employee == null) throw new InvalidOperationException("Employee not found");

                employee.PasswordHash = HashPassword(newPassword);
                await _repository.UpdateAsync(employee);
                await _unitOfWork.SaveChangesAsync();
            });
        }

        public async Task UpdateLastLoginAsync(int employeeId)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var employee = await _repository.GetByIdAsync(employeeId);
                if (employee != null)
                {
                    employee.LastLogin = DateTime.Now;
                    await _repository.UpdateAsync(employee);
                    await _unitOfWork.SaveChangesAsync();
                }
            });
        }

        public override async Task<EmployeeDTO> CreateAsync(EmployeeDTO dto)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                // Validate username uniqueness
                var existingEmployee = await GetByUsernameAsync(dto.Username);
                if (existingEmployee != null)
                {
                    throw new InvalidOperationException("Username already exists");
                }

                var entity = _mapper.Map<Employee>(dto);
                entity.CreatedAt = DateTime.Now;

                var result = await _repository.AddAsync(entity);
                await _unitOfWork.SaveChangesAsync();

                var resultDto = _mapper.Map<EmployeeDTO>(result);
                _eventAggregator.Publish(new EntityChangedEvent<EmployeeDTO>("Create", resultDto));

                return resultDto;
            });
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        public override async Task<IEnumerable<EmployeeDTO>> GetAllAsync()
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var employees = await _repository.Query()
                    .Include(e => e.SalaryTransactions)
                    .OrderBy(e => e.FirstName)
                    .ToListAsync();
                return _mapper.Map<IEnumerable<EmployeeDTO>>(employees);
            });
        }

        public override async Task<EmployeeDTO?> GetByIdAsync(int id)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var employee = await _repository.Query()
                    .Include(e => e.SalaryTransactions)
                    .FirstOrDefaultAsync(e => e.EmployeeId == id);
                return _mapper.Map<EmployeeDTO>(employee);
            });
        }
    }
}