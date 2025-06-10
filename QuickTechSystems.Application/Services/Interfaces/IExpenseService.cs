using QuickTechSystems.Application.DTOs;

namespace QuickTechSystems.Application.Services.Interfaces
{
    public interface IExpenseService : IBaseService<ExpenseDTO>
    {
        Task<decimal> GetTotalExpensesAsync(DateTime startDate, DateTime endDate);
        Task<IEnumerable<ExpenseDTO>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<IEnumerable<ExpenseDTO>> GetByCategoryAsync(string category);
        Task<bool> ValidateExpenseAsync(decimal amount);
        Task CreateEmployeeSalaryExpenseAsync(int employeeId, decimal amount, string employeeName);
        Task<Dictionary<string, decimal>> GetCategoryTotalsAsync(DateTime startDate, DateTime endDate);
    }
}