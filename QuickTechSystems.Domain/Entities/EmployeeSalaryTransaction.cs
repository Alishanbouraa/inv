using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickTechSystems.Domain.Entities
{
    public class EmployeeSalaryTransaction
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public decimal Amount { get; set; }
        public string TransactionType { get; set; } = string.Empty; // "Salary" or "Withdrawal"
        public DateTime TransactionDate { get; set; }
        public string? Notes { get; set; }
        public virtual Employee Employee { get; set; } = null!;
    }
}
