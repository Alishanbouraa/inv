using System;
using System.Collections.Generic;
using System.Linq;

namespace QuickTechSystems.Domain.Entities
{
    public class Drawer
    {
        // Primary Key
        public int DrawerId { get; set; }

        // Balance Properties
        public decimal OpeningBalance { get; set; }
        public decimal CurrentBalance { get; set; }
        public decimal CashIn { get; set; }
        public decimal CashOut { get; set; }

        // Total Transaction Properties
        public decimal TotalSales { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal TotalSupplierPayments { get; set; }

        // Net Calculation Properties
        public decimal NetCashFlow { get; set; }

        // Daily Transaction Properties
        public decimal DailySales { get; set; }
        public decimal DailyExpenses { get; set; }
        public decimal DailySupplierPayments { get; set; }

        // Timestamp Properties
        public DateTime OpenedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime LastUpdated { get; set; }
        public decimal NetSales { get; set; }
        // Status Properties
        public string Status { get; set; } = "Open";
        public string? Notes { get; set; }

        // Cashier Properties
        public string CashierId { get; set; } = string.Empty;
        public string CashierName { get; set; } = string.Empty;

        // Navigation Properties
        public virtual ICollection<DrawerTransaction> Transactions { get; set; } = new List<DrawerTransaction>();

        // Computed Properties
        public decimal ExpectedBalance => OpeningBalance + CashIn - CashOut;
        public decimal Difference => CurrentBalance - ExpectedBalance;

        // Helper Methods
        public void UpdateNetCalculations()
        {
            NetCashFlow = TotalSales - TotalExpenses - TotalSupplierPayments;
        }

        public void UpdateDailyCalculations()
        {
            var today = DateTime.Today;
            var todayTransactions = Transactions.Where(t => t.Timestamp.Date == today);

            DailySales = todayTransactions.Where(t => t.Type == "Cash Sale")
                .Sum(t => Math.Abs(t.Amount));

            DailyExpenses = todayTransactions.Where(t => t.Type == "Expense")
                .Sum(t => Math.Abs(t.Amount));

            DailySupplierPayments = todayTransactions.Where(t => t.Type == "Supplier Payment")
                .Sum(t => Math.Abs(t.Amount));
        }

        public void ResetDailyTotals()
        {
            DailySales = 0;
            DailyExpenses = 0;
            DailySupplierPayments = 0;
        }

        public bool HasDiscrepancy()
        {
            return Math.Abs(Difference) > 0.01m;
        }

        public bool IsWithinOperatingHours()
        {
            var now = DateTime.Now.TimeOfDay;
            var start = TimeSpan.FromHours(6); // 6 AM
            var end = TimeSpan.FromHours(23);  // 11 PM
            return now >= start && now <= end;
        }

        public TimeSpan GetDuration()
        {
            return Status.Equals("Closed", StringComparison.OrdinalIgnoreCase) && ClosedAt.HasValue
                ? ClosedAt.Value - OpenedAt
                : DateTime.Now - OpenedAt;
        }
    }
}