using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickTechSystems.Application.Events
{
    public class SupplierPaymentEvent
    {
        public decimal Amount { get; }
        public string SupplierName { get; }
        public string Reference { get; }

        public SupplierPaymentEvent(decimal amount, string supplierName, string reference)
        {
            Amount = amount;
            SupplierName = supplierName;
            Reference = reference;
        }
    }
}
