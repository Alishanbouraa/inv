// Update QuickTechSystems.Application/Services/Interfaces/IPrinterService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// IPrinterService.cs
using System.Drawing;

namespace QuickTechSystems.Application.Services.Interfaces
{
    public interface IPrinterService
    {
        void PrintBarcode(byte[] barcodeImage, string productName, string price, string barcodeText);
        List<string> GetInstalledPrinters();
        void SetPrinter(string printerName);
    }
}