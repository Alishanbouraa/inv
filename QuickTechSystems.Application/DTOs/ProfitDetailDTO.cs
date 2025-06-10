using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickTechSystems.Application.DTOs
{
    public class ProfitDetailDTO
    {
        public DateTime Date { get; set; }
        public decimal Sales { get; set; }
        public decimal Cost { get; set; }
        public int TransactionCount { get; set; }

        public int NetProfit { get; set; }
        public int ItemsCount { get; set; }
        public decimal GrossProfit => Sales - Cost;
        public decimal ProfitMargin => Sales > 0 ? GrossProfit / Sales : 0;
    }
}
