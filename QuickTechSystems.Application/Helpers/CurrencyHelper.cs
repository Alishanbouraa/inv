using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickTechSystems.Application.Helpers
{
    // Create new file: Application/Helpers/CurrencyHelper.cs
    public static class CurrencyHelper
    {
        private static decimal _exchangeRate = 100000m;

        public static void UpdateExchangeRate(decimal rate)
        {
            _exchangeRate = rate;
        }

        public static decimal ConvertToLBP(decimal usdAmount)
        {
            return usdAmount * _exchangeRate;
        }

        public static string FormatLBP(decimal lbpAmount)
        {
            return $"{lbpAmount:N0} LBP";
        }

        public static string FormatDualCurrency(decimal usdAmount)
        {
            decimal lbpAmount = ConvertToLBP(usdAmount);
            return $"${usdAmount:N2} / {FormatLBP(lbpAmount)}";
        }
    }
}
