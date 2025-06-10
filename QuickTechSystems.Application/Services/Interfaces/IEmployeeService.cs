using QuickTechSystems.Application.DTOs;

namespace QuickTechSystems.Application.Services.Interfaces
{
    public interface IEmployeeService : IBaseService<EmployeeDTO>
    {
        Task<EmployeeDTO?> GetByUsernameAsync(string username);
        Task ResetPasswordAsync(int employeeId, string newPassword);
        Task UpdateAsync(int employeeId, EmployeeDTO dto);
        Task UpdateLastLoginAsync(int employeeId);

        // Add these new methods
        Task ProcessMonthlySalaryAsync(int employeeId);
        Task<bool> ProcessSalaryWithdrawalAsync(int employeeId, decimal amount, string notes);
        Task<IEnumerable<EmployeeSalaryTransactionDTO>> GetSalaryHistoryAsync(int employeeId);
    }
}