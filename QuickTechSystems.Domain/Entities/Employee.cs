using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickTechSystems.Domain.Entities
{
    // Domain/Entities/Employee.cs
    public class Employee
    {
        public int EmployeeId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // Admin, Cashier, Manager
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLogin { get; set; }
        public decimal MonthlySalary { get; set; }
        public decimal CurrentBalance { get; set; }
        public virtual ICollection<EmployeeSalaryTransaction> SalaryTransactions { get; set; }
            = new List<EmployeeSalaryTransaction>();
    }
}
