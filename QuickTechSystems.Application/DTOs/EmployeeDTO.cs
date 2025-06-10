using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickTechSystems.Application.DTOs
{
    // Application/DTOs/EmployeeDTO.cs
    public class EmployeeDTO : BaseDTO
    {
        public int EmployeeId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime? LastLogin { get; set; }
        public string FullName => $"{FirstName} {LastName}";
        public decimal MonthlySalary { get; set; }
        public decimal CurrentBalance { get; set; }
    }
}
