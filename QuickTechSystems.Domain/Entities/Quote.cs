using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickTechSystems.Domain.Entities
{
    public class Quote
    {
        public int QuoteId { get; set; }
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Converted, Expired
        public string QuoteNumber { get; set; } = string.Empty;

        // Navigation properties
        public virtual Customer? Customer { get; set; }
        public virtual ICollection<QuoteDetail> QuoteDetails { get; set; } = new List<QuoteDetail>();
    }

    public class QuoteDetail
    {
        public int QuoteDetailId { get; set; }
        public int QuoteId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal Total { get; set; }

        public virtual Quote Quote { get; set; } = null!;
        public virtual Product Product { get; set; } = null!;
    }
}
