// Path: QuickTechSystems.Application.Services.Interfaces/IBarcodeService.cs
namespace QuickTechSystems.Application.Services.Interfaces
{
    public interface IBarcodeService
    {
        /// <summary>
        /// Generates a barcode image as byte array from the provided content string
        /// </summary>
        /// <param name="content">The text/number to encode in the barcode</param>
        /// <param name="width">Width of the generated barcode image (default: 300)</param>
        /// <param name="height">Height of the generated barcode image (default: 100)</param>
        /// <returns>Byte array containing the PNG image data of the generated barcode</returns>
        byte[] GenerateBarcode(string content, int width = 300, int height = 100);
    }
}