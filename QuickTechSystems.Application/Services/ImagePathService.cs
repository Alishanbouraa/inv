using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Drawing;
using System.Drawing.Imaging;

namespace QuickTechSystems.Application.Services
{
    public interface IImagePathService
    {
        string GetProductImagesDirectory();
        string SaveProductImage(string sourcePath);
        Task<string> SaveProductImageAsync(string sourcePath);
        bool DeleteProductImage(string imagePath);
        string GetFullImagePath(string relativePath);
        bool IsValidImageFile(string filePath);
    }

    public class ImagePathService : IImagePathService
    {
        private readonly string _baseProductImagePath;
        private readonly IConfiguration _configuration;

        public ImagePathService(IConfiguration configuration)
        {
            _configuration = configuration;

            try
            {
                // Get image storage path from configuration or use default
                string configPath = null;

                // First, try to get it from configuration section
                var imageStorageSection = configuration.GetSection("ImageStorage");
                if (imageStorageSection.Exists())
                {
                    configPath = imageStorageSection["ProductImagesPath"];
                }

                if (!string.IsNullOrEmpty(configPath))
                {
                    _baseProductImagePath = configPath;
                }
                else
                {
                    // Default to application directory
                    _baseProductImagePath = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "ProductImages"
                    );
                }

                // Ensure directory exists
                Directory.CreateDirectory(_baseProductImagePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing ImagePathService: {ex.Message}");
                // Fallback to a safe path
                _baseProductImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ProductImages");
                Directory.CreateDirectory(_baseProductImagePath);
            }
        }

        public string GetProductImagesDirectory()
        {
            return _baseProductImagePath;
        }

        public string SaveProductImage(string sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath))
                throw new ArgumentNullException(nameof(sourcePath), "Source path cannot be null or empty");

            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Source image file not found", sourcePath);

            if (!IsValidImageFile(sourcePath))
                throw new ArgumentException("The file is not a valid image", nameof(sourcePath));

            try
            {
                // Generate unique filename using timestamp and original filename
                var originalFileName = Path.GetFileName(sourcePath);
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

                // Add a unique identifier to prevent collisions
                var randomPart = Path.GetRandomFileName().Substring(0, 8);

                var uniqueFileName = $"{timestamp}_{randomPart}_{originalFileName}";
                var destinationPath = Path.Combine(_baseProductImagePath, uniqueFileName);

                // Copy the file
                File.Copy(sourcePath, destinationPath, true);

                // Return the relative path to store in the database
                return uniqueFileName;
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"IO Error saving image: {ex.Message}");
                throw new IOException($"Failed to save image: {ex.Message}", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Access denied when saving image: {ex.Message}");
                throw new UnauthorizedAccessException($"Access denied when saving image: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving image: {ex.Message}");
                throw new Exception($"Failed to save image: {ex.Message}", ex);
            }
        }

        public async Task<string> SaveProductImageAsync(string sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath))
                throw new ArgumentNullException(nameof(sourcePath), "Source path cannot be null or empty");

            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Source image file not found", sourcePath);

            if (!IsValidImageFile(sourcePath))
                throw new ArgumentException("The file is not a valid image", nameof(sourcePath));

            try
            {
                // Generate unique filename using timestamp and original filename
                var originalFileName = Path.GetFileName(sourcePath);
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

                // Add a unique identifier to prevent collisions
                var randomPart = Path.GetRandomFileName().Substring(0, 8);

                var uniqueFileName = $"{timestamp}_{randomPart}_{originalFileName}";
                var destinationPath = Path.Combine(_baseProductImagePath, uniqueFileName);

                // Copy the file asynchronously
                using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
                using (var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                {
                    await sourceStream.CopyToAsync(destinationStream);
                }

                // Return the relative path to store in the database
                return uniqueFileName;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving image asynchronously: {ex.Message}");
                throw new Exception($"Failed to save image: {ex.Message}", ex);
            }
        }

        public bool DeleteProductImage(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
                return false;

            try
            {
                var fullPath = GetFullImagePath(imagePath);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    return true;
                }
                return false;
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"IO Error deleting image: {ex.Message}");
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Access denied when deleting image: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting image: {ex.Message}");
                return false;
            }
        }

        public string GetFullImagePath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return string.Empty;

            if (Path.IsPathRooted(relativePath))
                return relativePath;

            return Path.Combine(_baseProductImagePath, relativePath);
        }

        public bool IsValidImageFile(string filePath)
        {
            try
            {
                // Try to validate by file extension first (fast check)
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                if (string.IsNullOrEmpty(extension) ||
                    (extension != ".jpg" && extension != ".jpeg" &&
                     extension != ".png" && extension != ".gif" &&
                     extension != ".bmp"))
                {
                    return false;
                }

                // For stricter validation (if needed), try to load the image
                // Uncomment this if you want more thorough validation
                /*
                using (var img = Image.FromFile(filePath))
                {
                    return img != null;
                }
                */

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}