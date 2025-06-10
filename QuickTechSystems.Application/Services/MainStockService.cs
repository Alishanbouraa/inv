// Path: QuickTechSystems.Application.Services/MainStockService.cs
using System.Diagnostics;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Interfaces;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.Domain.Entities;
using QuickTechSystems.Domain.Interfaces.Repositories;

namespace QuickTechSystems.Application.Services
{
    public class MainStockService : BaseService<MainStock, MainStockDTO>, IMainStockService
    {
        private readonly IInventoryTransferService _inventoryTransferService;
        private readonly IProductService _productService;

        public MainStockService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IEventAggregator eventAggregator,
            IDbContextScopeService dbContextScopeService,
            IInventoryTransferService inventoryTransferService,
            IProductService productService)
            : base(unitOfWork, mapper, eventAggregator, dbContextScopeService)
        {
            _inventoryTransferService = inventoryTransferService;
            _productService = productService;
        }

        public override async Task<IEnumerable<MainStockDTO>> GetAllAsync()
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                Debug.WriteLine("MainStockService: Retrieving all MainStock items with category and supplier details");

                // Include Category and Supplier to ensure we can map names
                var items = await _repository.Query()
                    .Include(m => m.Category)
                    .Include(m => m.Supplier)
                    .ToListAsync();

                var dtos = _mapper.Map<IEnumerable<MainStockDTO>>(items);

                // Manually set the names since they don't exist in entities
                foreach (var dto in dtos)
                {
                    var item = items.FirstOrDefault(i => i.MainStockId == dto.MainStockId);
                    if (item != null)
                    {
                        if (item.Category != null)
                        {
                            dto.CategoryName = item.Category.Name;
                        }

                        if (item.Supplier != null)
                        {
                            dto.SupplierName = item.Supplier.Name;
                        }
                    }
                }

                return dtos;
            });
        }

        public async Task<IEnumerable<MainStockDTO>> GetByCategoryAsync(int categoryId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var products = await _repository.Query()
                    .Include(p => p.Category)
                    .Include(p => p.Supplier)
                    .Where(p => p.CategoryId == categoryId)
                    .ToListAsync();

                var dtos = _mapper.Map<IEnumerable<MainStockDTO>>(products);

                // Manually set names
                foreach (var dto in dtos)
                {
                    var product = products.FirstOrDefault(p => p.MainStockId == dto.MainStockId);
                    if (product != null)
                    {
                        if (product.Category != null)
                        {
                            dto.CategoryName = product.Category.Name;
                        }

                        if (product.Supplier != null)
                        {
                            dto.SupplierName = product.Supplier.Name;
                        }
                    }
                }

                return dtos;
            });
        }

        public async Task<IEnumerable<MainStockDTO>> GetLowStockProductsAsync()
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var products = await _repository.Query()
                    .Include(p => p.Category)
                    .Include(p => p.Supplier)
                    .Where(p => p.CurrentStock <= p.MinimumStock)
                    .ToListAsync();

                var dtos = _mapper.Map<IEnumerable<MainStockDTO>>(products);

                // Manually set names
                foreach (var dto in dtos)
                {
                    var product = products.FirstOrDefault(p => p.MainStockId == dto.MainStockId);
                    if (product != null)
                    {
                        if (product.Category != null)
                        {
                            dto.CategoryName = product.Category.Name;
                        }

                        if (product.Supplier != null)
                        {
                            dto.SupplierName = product.Supplier.Name;
                        }
                    }
                }

                return dtos;
            });
        }

        public async Task<MainStockDTO> FindProductByBarcodeAsync(string barcode, int excludeMainStockId = 0)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var query = _repository.Query()
                    .Include(p => p.Category)
                    .Include(p => p.Supplier)
                    .AsNoTracking() // Important: Prevent tracking conflicts
                    .Where(p => p.Barcode == barcode);

                if (excludeMainStockId > 0)
                {
                    query = query.Where(p => p.MainStockId != excludeMainStockId);
                }

                var product = await query.FirstOrDefaultAsync();
                var dto = _mapper.Map<MainStockDTO>(product);

                if (product != null)
                {
                    if (product.Category != null)
                    {
                        dto.CategoryName = product.Category.Name;
                    }

                    if (product.Supplier != null)
                    {
                        dto.SupplierName = product.Supplier.Name;
                    }
                }

                return dto;
            });
        }
        public async Task<MainStockDTO?> GetByBoxBarcodeAsync(string boxBarcode)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var product = await _repository.Query()
                    .Include(p => p.Category)
                    .Include(p => p.Supplier)
                    .FirstOrDefaultAsync(p => p.BoxBarcode == boxBarcode);

                var dto = _mapper.Map<MainStockDTO>(product);

                if (product != null)
                {
                    if (product.Category != null)
                    {
                        dto.CategoryName = product.Category.Name;
                    }

                    if (product.Supplier != null)
                    {
                        dto.SupplierName = product.Supplier.Name;
                    }
                }

                return dto;
            });
        }

        public async Task<bool> UpdateStockAsync(int mainStockId, decimal quantity)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    Debug.WriteLine($"Updating stock for MainStock {mainStockId} by {quantity}");

                    var product = await _repository.GetByIdAsync(mainStockId);
                    if (product == null)
                    {
                        Debug.WriteLine($"MainStock {mainStockId} not found for stock update");
                        return false;
                    }

                    // Store original stock for logging
                    decimal oldStock = product.CurrentStock;

                    // Calculate new stock - ensure we use exact decimal math with no rounding
                    product.CurrentStock = decimal.Add(product.CurrentStock, quantity);
                    product.UpdatedAt = DateTime.Now;

                    await _repository.UpdateAsync(product);
                    await _unitOfWork.SaveChangesAsync();

                    Debug.WriteLine($"Stock updated for MainStock {mainStockId}: {oldStock} → {product.CurrentStock}");

                    // Get product with relationships for proper mapping
                    var updatedProduct = await _repository.Query()
                        .Include(p => p.Category)
                        .Include(p => p.Supplier)
                        .FirstOrDefaultAsync(p => p.MainStockId == mainStockId);

                    // Publish update event
                    var productDto = _mapper.Map<MainStockDTO>(updatedProduct ?? product);

                    // Set names manually
                    if (updatedProduct != null)
                    {
                        if (updatedProduct.Category != null)
                        {
                            productDto.CategoryName = updatedProduct.Category.Name;
                        }

                        if (updatedProduct.Supplier != null)
                        {
                            productDto.SupplierName = updatedProduct.Supplier.Name;
                        }
                    }

                    _eventAggregator.Publish(new EntityChangedEvent<MainStockDTO>("Update", productDto));

                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error updating MainStock: {ex.Message}");
                    throw;
                }
            });
        }

        public override async Task<MainStockDTO> CreateAsync(MainStockDTO dto)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                Debug.WriteLine("Starting create in MainStockService");

                if (dto.CreatedAt == default)
                {
                    dto.CreatedAt = DateTime.Now;
                }

                if (string.IsNullOrWhiteSpace(dto.BoxBarcode) && !string.IsNullOrWhiteSpace(dto.Barcode))
                {
                    dto.BoxBarcode = $"BX{dto.Barcode}";
                }

                if (string.IsNullOrWhiteSpace(dto.CategoryName) && dto.CategoryId > 0)
                {
                    var category = await _unitOfWork.Categories.GetByIdAsync(dto.CategoryId);
                    if (category != null)
                    {
                        dto.CategoryName = category.Name;
                        Debug.WriteLine($"Set CategoryName to {category.Name} for item {dto.Name}");
                    }
                }

                if (string.IsNullOrWhiteSpace(dto.SupplierName) && dto.SupplierId.HasValue && dto.SupplierId.Value > 0)
                {
                    var supplier = await _unitOfWork.Suppliers.GetByIdAsync(dto.SupplierId.Value);
                    if (supplier != null)
                    {
                        dto.SupplierName = supplier.Name;
                        Debug.WriteLine($"Set SupplierName to {supplier.Name} for item {dto.Name}");
                    }
                }

                var entity = _mapper.Map<MainStock>(dto);

                if (dto.AutoSyncToProducts)
                {
                    entity.CurrentStock = 0;
                    entity.NumberOfBoxes = 0;
                }
                else
                {
                    entity.CurrentStock = dto.IndividualItems;
                    entity.NumberOfBoxes = dto.NumberOfBoxes;
                }

                entity.ItemsPerBox = dto.ItemsPerBox;

                var result = await _repository.AddAsync(entity);
                await _unitOfWork.SaveChangesAsync();

                if (result.MainStockId <= 0)
                {
                    Debug.WriteLine($"WARNING: MainStockId was not properly assigned for {dto.Name}");
                    throw new InvalidOperationException($"Database did not assign a valid ID to MainStock item: {dto.Name}");
                }

                var freshEntity = await _repository.Query()
                    .Include(m => m.Category)
                    .Include(m => m.Supplier)
                    .FirstOrDefaultAsync(m => m.MainStockId == result.MainStockId);

                var resultDto = _mapper.Map<MainStockDTO>(freshEntity ?? result);

                if (freshEntity != null)
                {
                    if (freshEntity.Category != null)
                        resultDto.CategoryName = freshEntity.Category.Name;
                    if (freshEntity.Supplier != null)
                        resultDto.SupplierName = freshEntity.Supplier.Name;
                }

                if (dto.AutoSyncToProducts)
                {
                    var originalCurrentStock = dto.CurrentStock;
                    resultDto.CurrentStock = originalCurrentStock;
                    await AutoSyncToProductsAsync(resultDto);
                    resultDto.CurrentStock = 0;
                }

                Debug.WriteLine($"Successfully created MainStock item with ID: {resultDto.MainStockId}");
                _eventAggregator.Publish(new EntityChangedEvent<MainStockDTO>("Create", resultDto));

                return resultDto;
            });
        }
        private async Task AutoSyncToProductsAsync(MainStockDTO mainStockDto)
        {
            try
            {
                var existingProduct = await _productService.FindProductByBarcodeAsync(mainStockDto.Barcode);

                if (existingProduct != null)
                {
                    var updatedProduct = new ProductDTO
                    {
                        ProductId = existingProduct.ProductId,
                        Name = mainStockDto.Name,
                        Barcode = mainStockDto.Barcode,
                        BoxBarcode = mainStockDto.BoxBarcode,
                        CategoryId = mainStockDto.CategoryId,
                        CategoryName = mainStockDto.CategoryName,
                        SupplierId = mainStockDto.SupplierId,
                        SupplierName = mainStockDto.SupplierName,
                        Description = mainStockDto.Description,
                        PurchasePrice = mainStockDto.PurchasePrice,
                        WholesalePrice = mainStockDto.WholesalePrice,
                        SalePrice = mainStockDto.SalePrice,
                        MainStockId = mainStockDto.MainStockId,
                        BoxPurchasePrice = mainStockDto.BoxPurchasePrice,
                        BoxWholesalePrice = mainStockDto.BoxWholesalePrice,
                        BoxSalePrice = mainStockDto.BoxSalePrice,
                        ItemsPerBox = mainStockDto.ItemsPerBox,
                        MinimumBoxStock = mainStockDto.MinimumBoxStock,
                        CurrentStock = existingProduct.CurrentStock + (int)mainStockDto.CurrentStock,
                        MinimumStock = mainStockDto.MinimumStock,
                        ImagePath = mainStockDto.ImagePath,
                        Speed = mainStockDto.Speed,
                        IsActive = mainStockDto.IsActive,
                        UpdatedAt = DateTime.Now
                    };

                    await _productService.UpdateAsync(updatedProduct);
                }
                else
                {
                    var newProduct = new ProductDTO
                    {
                        Name = mainStockDto.Name,
                        Barcode = mainStockDto.Barcode,
                        BoxBarcode = mainStockDto.BoxBarcode,
                        CategoryId = mainStockDto.CategoryId,
                        CategoryName = mainStockDto.CategoryName,
                        SupplierId = mainStockDto.SupplierId,
                        SupplierName = mainStockDto.SupplierName,
                        Description = mainStockDto.Description,
                        PurchasePrice = mainStockDto.PurchasePrice,
                        WholesalePrice = mainStockDto.WholesalePrice,
                        SalePrice = mainStockDto.SalePrice,
                        MainStockId = mainStockDto.MainStockId,
                        BoxPurchasePrice = mainStockDto.BoxPurchasePrice,
                        BoxWholesalePrice = mainStockDto.BoxWholesalePrice,
                        BoxSalePrice = mainStockDto.BoxSalePrice,
                        ItemsPerBox = mainStockDto.ItemsPerBox,
                        MinimumBoxStock = mainStockDto.MinimumBoxStock,
                        CurrentStock = (int)mainStockDto.CurrentStock,
                        MinimumStock = mainStockDto.MinimumStock,
                        ImagePath = mainStockDto.ImagePath,
                        Speed = mainStockDto.Speed,
                        IsActive = mainStockDto.IsActive,
                        CreatedAt = DateTime.Now
                    };

                    await _productService.CreateAsync(newProduct);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error auto-syncing to Products: {ex.Message}");
            }
        }
        public async Task<MainStockDTO?> GetByBarcodeAsync(string barcode)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var product = await _repository.Query()
                    .Include(p => p.Category)
                    .Include(p => p.Supplier)
                    .FirstOrDefaultAsync(p => p.Barcode == barcode);

                var dto = _mapper.Map<MainStockDTO>(product);

                if (product != null)
                {
                    if (product.Category != null)
                    {
                        dto.CategoryName = product.Category.Name;
                    }

                    if (product.Supplier != null)
                    {
                        dto.SupplierName = product.Supplier.Name;
                    }
                }

                return dto;
            });
        }

        public async Task<MainStockDTO> GetByIdAsync(int id)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var mainStock = await _repository.Query()
                    .Include(m => m.Category)
                    .Include(m => m.Supplier)
                    .FirstOrDefaultAsync(m => m.MainStockId == id);

                var dto = _mapper.Map<MainStockDTO>(mainStock);

                if (mainStock != null)
                {
                    if (mainStock.Category != null)
                    {
                        dto.CategoryName = mainStock.Category.Name;
                    }

                    if (mainStock.Supplier != null)
                    {
                        dto.SupplierName = mainStock.Supplier.Name;
                    }
                }

                return dto;
            });
        }

        // Path: QuickTechSystems.Application.Services/MainStockService.cs
        public async Task<List<MainStockDTO>> CreateBatchAsync(List<MainStockDTO> products, IProgress<string>? progress = null)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var savedProducts = new List<MainStockDTO>();
                var categories = await _unitOfWork.Categories.Query().AsNoTracking().ToListAsync();
                var suppliers = await _unitOfWork.Suppliers.Query().AsNoTracking().ToListAsync();
                bool shouldAutoSync = products.FirstOrDefault()?.AutoSyncToProducts ?? false;

                for (int i = 0; i < products.Count; i++)
                {
                    var product = products[i];
                    try
                    {
                        progress?.Report($"Processing item {i + 1} of {products.Count}: {product.Name}");

                        if (string.IsNullOrEmpty(product.CategoryName) && product.CategoryId > 0)
                            product.CategoryName = categories.FirstOrDefault(c => c.CategoryId == product.CategoryId)?.Name ?? string.Empty;

                        if (string.IsNullOrEmpty(product.SupplierName) && product.SupplierId.HasValue && product.SupplierId.Value > 0)
                            product.SupplierName = suppliers.FirstOrDefault(s => s.SupplierId == product.SupplierId.Value)?.Name ?? string.Empty;

                        if (product.CreatedAt == default)
                            product.CreatedAt = DateTime.Now;

                        if (string.IsNullOrWhiteSpace(product.BoxBarcode) && !string.IsNullOrWhiteSpace(product.Barcode))
                            product.BoxBarcode = $"BX{product.Barcode}";

                        // REMOVED: The problematic line that forced ItemsPerBox to 1
                        // Allow ItemsPerBox to be 0 for items that aren't sold in boxes
                        // if (product.ItemsPerBox <= 0)
                        //     product.ItemsPerBox = 1;

                        var existingProduct = !string.IsNullOrEmpty(product.Barcode)
                            ? await _repository.Query().FirstOrDefaultAsync(p => p.Barcode == product.Barcode)
                            : null;

                        if (existingProduct != null)
                        {
                            product.MainStockId = existingProduct.MainStockId;
                            var entity = _mapper.Map<MainStock>(product);
                            entity.CreatedAt = existingProduct.CreatedAt;
                            entity.UpdatedAt = DateTime.Now;

                            if (shouldAutoSync)
                            {
                                entity.CurrentStock = 0;
                                entity.NumberOfBoxes = 0;
                            }

                            _unitOfWork.DetachEntity(existingProduct);
                            await _repository.UpdateAsync(entity);
                            await _unitOfWork.SaveChangesAsync();

                            var refreshedEntity = await _repository.Query()
                                .Include(m => m.Category)
                                .Include(m => m.Supplier)
                                .AsNoTracking()
                                .FirstOrDefaultAsync(m => m.MainStockId == entity.MainStockId);

                            var updatedDto = _mapper.Map<MainStockDTO>(refreshedEntity ?? entity);
                            UpdateDtoNames(updatedDto, refreshedEntity, product);

                            if (shouldAutoSync)
                            {
                                var originalCurrentStock = product.CurrentStock;
                                updatedDto.CurrentStock = originalCurrentStock;
                                await AutoSyncToProductsAsync(updatedDto);
                                updatedDto.CurrentStock = 0;
                            }

                            savedProducts.Add(updatedDto);
                            _eventAggregator.Publish(new EntityChangedEvent<MainStockDTO>("Update", updatedDto));
                        }
                        else
                        {
                            var entity = _mapper.Map<MainStock>(product);

                            if (shouldAutoSync)
                            {
                                entity.CurrentStock = 0;
                                entity.NumberOfBoxes = 0;
                            }

                            var addedEntity = await _repository.AddAsync(entity);
                            await _unitOfWork.SaveChangesAsync();

                            if (addedEntity.MainStockId <= 0)
                                throw new InvalidOperationException($"Database did not assign a valid ID to new MainStock: {product.Name}");

                            var freshEntity = await _repository.Query()
                                .Include(m => m.Category)
                                .Include(m => m.Supplier)
                                .AsNoTracking()
                                .FirstOrDefaultAsync(m => m.MainStockId == addedEntity.MainStockId);

                            var newDto = _mapper.Map<MainStockDTO>(freshEntity ?? addedEntity);
                            UpdateDtoNames(newDto, freshEntity, product);

                            if (shouldAutoSync)
                            {
                                var originalCurrentStock = product.CurrentStock;
                                newDto.CurrentStock = originalCurrentStock;
                                await AutoSyncToProductsAsync(newDto);
                                newDto.CurrentStock = 0;
                            }

                            savedProducts.Add(newDto);
                            _eventAggregator.Publish(new EntityChangedEvent<MainStockDTO>("Create", newDto));
                        }

                        if (product.MainStockId > 0)
                            await SyncLinkedProductsForMainStock(product.MainStockId, product);

                        if (i % 5 == 0)
                            _unitOfWork.Context.ChangeTracker.Clear();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing item {i + 1} ({product.Name}): {ex.Message}");
                        throw;
                    }
                }
                return savedProducts;
            });
        }

        private void UpdateDtoNames(MainStockDTO dto, MainStock entity, MainStockDTO fallback)
        {
            if (entity != null)
            {
                if (entity.Category != null)
                    dto.CategoryName = entity.Category.Name;
                if (entity.Supplier != null)
                    dto.SupplierName = entity.Supplier.Name;
            }
            else
            {
                dto.CategoryName = fallback.CategoryName;
                dto.SupplierName = fallback.SupplierName;
            }
        }

        // Private helper method for direct database operations
        private async Task UpdateMainStockDirectly(MainStock item)
        {
            string sql = @"
        UPDATE MainStocks
        SET Name = @name,
            Barcode = @barcode,
            BoxBarcode = @boxBarcode,
            CategoryId = @categoryId,
            SupplierId = @supplierId,
            Description = @description,
            PurchasePrice = @purchasePrice,
            SalePrice = @salePrice,
            CurrentStock = @currentStock,
            MinimumStock = @minimumStock,
            Speed = @speed,
            BoxPurchasePrice = @boxPurchasePrice,
            BoxSalePrice = @boxSalePrice,
            ItemsPerBox = @itemsPerBox,
            NumberOfBoxes = @numberOfBoxes,
            MinimumBoxStock = @minimumBoxStock,
            IsActive = @isActive,
            UpdatedAt = @updatedAt
        WHERE MainStockId = @mainStockId";

            var parameters = new[]
            {
                new Microsoft.Data.SqlClient.SqlParameter("@mainStockId", item.MainStockId),
                new Microsoft.Data.SqlClient.SqlParameter("@name", item.Name),
                new Microsoft.Data.SqlClient.SqlParameter("@barcode", item.Barcode),
                new Microsoft.Data.SqlClient.SqlParameter("@boxBarcode", (object)item.BoxBarcode ?? DBNull.Value),
                new Microsoft.Data.SqlClient.SqlParameter("@categoryId", item.CategoryId),
                new Microsoft.Data.SqlClient.SqlParameter("@supplierId", (object)item.SupplierId ?? DBNull.Value),
                new Microsoft.Data.SqlClient.SqlParameter("@description", (object)item.Description ?? DBNull.Value),
                new Microsoft.Data.SqlClient.SqlParameter("@purchasePrice", item.PurchasePrice),
                new Microsoft.Data.SqlClient.SqlParameter("@salePrice", item.SalePrice),
                new Microsoft.Data.SqlClient.SqlParameter("@currentStock", item.CurrentStock),
                new Microsoft.Data.SqlClient.SqlParameter("@minimumStock", item.MinimumStock),
                new Microsoft.Data.SqlClient.SqlParameter("@speed", (object)item.Speed ?? DBNull.Value),
                new Microsoft.Data.SqlClient.SqlParameter("@boxPurchasePrice", item.BoxPurchasePrice),
                new Microsoft.Data.SqlClient.SqlParameter("@boxSalePrice", item.BoxSalePrice),
                new Microsoft.Data.SqlClient.SqlParameter("@itemsPerBox", item.ItemsPerBox),
                new Microsoft.Data.SqlClient.SqlParameter("@numberOfBoxes", item.NumberOfBoxes),
                new Microsoft.Data.SqlClient.SqlParameter("@minimumBoxStock", item.MinimumBoxStock),
                new Microsoft.Data.SqlClient.SqlParameter("@isActive", item.IsActive),
                new Microsoft.Data.SqlClient.SqlParameter("@updatedAt", DateTime.Now)
            };

            await _unitOfWork.Context.Database.ExecuteSqlRawAsync(sql, parameters);
        }

        private async Task SyncLinkedProductsForMainStock(int mainStockId, MainStockDTO mainStockData)
        {
            try
            {
                // Use SQL to find linked products
                string findSql = "SELECT ProductId FROM Products WHERE MainStockId = @mainStockId";
                var findParam = new Microsoft.Data.SqlClient.SqlParameter("@mainStockId", mainStockId);

                var productIds = await _unitOfWork.Context.Set<Product>()
                    .FromSqlRaw(findSql, findParam)
                    .Select(p => p.ProductId)
                    .ToListAsync();

                if (productIds.Any())
                {
                    Debug.WriteLine($"Syncing {productIds.Count} products linked to MainStock {mainStockId}");

                    // Use direct SQL update to update all linked products at once
                    string updateSql = @"
                UPDATE Products
                SET PurchasePrice = @purchasePrice,
                    WholesalePrice = @wholesalePrice,
                    SalePrice = @salePrice,
                    BoxPurchasePrice = @boxPurchasePrice,
                    BoxWholesalePrice = @boxWholesalePrice,                  
                    BoxSalePrice = @boxSalePrice,
                    ItemsPerBox = @itemsPerBox,
                    UpdatedAt = @updatedAt
                WHERE MainStockId = @mainStockId";

                    var updateParams = new[]
                    {
                        new Microsoft.Data.SqlClient.SqlParameter("@mainStockId", mainStockId),
                        new Microsoft.Data.SqlClient.SqlParameter("@purchasePrice", mainStockData.PurchasePrice),
                        new Microsoft.Data.SqlClient.SqlParameter("@wholesalePrice", mainStockData.WholesalePrice),
                        new Microsoft.Data.SqlClient.SqlParameter("@salePrice", mainStockData.SalePrice),
                        new Microsoft.Data.SqlClient.SqlParameter("@boxPurchasePrice", mainStockData.BoxPurchasePrice),
                        new Microsoft.Data.SqlClient.SqlParameter("@boxWholesalePrice", mainStockData.BoxWholesalePrice),
                        new Microsoft.Data.SqlClient.SqlParameter("@boxSalePrice", mainStockData.BoxSalePrice),
                        new Microsoft.Data.SqlClient.SqlParameter("@itemsPerBox", mainStockData.ItemsPerBox),
                        new Microsoft.Data.SqlClient.SqlParameter("@updatedAt", DateTime.Now)
                    };

                    await _unitOfWork.Context.Database.ExecuteSqlRawAsync(updateSql, updateParams);

                    // Update linked products' DTOs
                    foreach (var productId in productIds)
                    {
                        var product = await _unitOfWork.Products
                            .Query()
                            .Include(p => p.Category)
                            .Include(p => p.Supplier)
                            .FirstOrDefaultAsync(p => p.ProductId == productId);

                        if (product != null)
                        {
                            var productDto = _mapper.Map<ProductDTO>(product);

                            // Set category and supplier names
                            if (product.Category != null)
                                productDto.CategoryName = product.Category.Name;
                            if (product.Supplier != null)
                                productDto.SupplierName = product.Supplier.Name;

                            _eventAggregator.Publish(new EntityChangedEvent<ProductDTO>("Update", productDto));
                        }
                        else
                        {
                            // Fallback to just publishing the stock update event
                            var updateEvent = new ProductStockUpdatedEvent(productId, mainStockData.CurrentStock);
                            _eventAggregator.Publish(updateEvent);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error syncing linked products: {ex.Message}");
                // Log but don't throw to allow the rest of the batch to continue
            }
        }

        public async Task<bool> TransferToStoreAsync(int mainStockId, int productId, decimal quantity, string transferredBy, string notes, bool isByBoxes = false)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    Debug.WriteLine($"Transfer details: MainStock ID: {mainStockId}, Product ID: {productId}, Quantity: {quantity}, By Boxes: {isByBoxes}");

                    // Get MainStock and Product data to ensure they exist
                    var mainStock = await _unitOfWork.MainStocks
                        .Query()
                        .Include(m => m.Category)
                        .Include(m => m.Supplier)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(m => m.MainStockId == mainStockId);

                    if (mainStock == null)
                    {
                        Debug.WriteLine($"MainStock with ID {mainStockId} not found");
                        return false;
                    }

                    var product = await _unitOfWork.Products
                        .Query()
                        .Include(p => p.Category)
                        .Include(p => p.Supplier)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.ProductId == productId);

                    if (product == null)
                    {
                        Debug.WriteLine($"Product with ID {productId} not found");
                        return false;
                    }

                    // Use database connection directly to avoid EF Core tracking completely
                    var connection = _unitOfWork.Context.Database.GetDbConnection();
                    if (connection.State != System.Data.ConnectionState.Open)
                        await connection.OpenAsync();

                    // Step 1: Verify MainStock has sufficient inventory
                    decimal currentStock = 0;
                    int numberOfBoxes = 0;
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction.GetDbTransaction();
                        command.CommandText = @"
                SELECT CurrentStock, NumberOfBoxes FROM MainStocks 
                WHERE MainStockId = @mainStockId";

                        var param = command.CreateParameter();
                        param.ParameterName = "@mainStockId";
                        param.Value = mainStockId;
                        command.Parameters.Add(param);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                currentStock = reader.GetDecimal(0);
                                numberOfBoxes = reader.GetInt32(1);
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }

                    // Check if there's enough stock
                    if (isByBoxes)
                    {
                        // Convert decimal quantity to int for box comparison
                        int boxQuantity = Convert.ToInt32(quantity);
                        if (numberOfBoxes < boxQuantity)
                        {
                            Debug.WriteLine($"Insufficient box stock: {numberOfBoxes} < {boxQuantity}");
                            return false;
                        }
                    }
                    else
                    {
                        if (currentStock < quantity)
                        {
                            Debug.WriteLine($"Insufficient item stock: {currentStock} < {quantity}");
                            return false;
                        }
                    }

                    // Step 2: Update MainStock - deduct inventory
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction.GetDbTransaction();

                        if (isByBoxes)
                        {
                            command.CommandText = @"
                    UPDATE MainStocks 
                    SET NumberOfBoxes = NumberOfBoxes - @quantity, 
                        UpdatedAt = @updatedAt
                    WHERE MainStockId = @mainStockId";

                            // For box transfers, we need to explicitly convert to int
                            var quantityParam = command.CreateParameter();
                            quantityParam.ParameterName = "@quantity";
                            quantityParam.Value = Convert.ToInt32(quantity);
                            command.Parameters.Add(quantityParam);
                        }
                        else
                        {
                            command.CommandText = @"
                    UPDATE MainStocks 
                    SET CurrentStock = CurrentStock - @quantity, 
                        UpdatedAt = @updatedAt
                    WHERE MainStockId = @mainStockId";

                            var quantityParam = command.CreateParameter();
                            quantityParam.ParameterName = "@quantity";
                            quantityParam.Value = quantity;
                            command.Parameters.Add(quantityParam);
                        }

                        var idParam = command.CreateParameter();
                        idParam.ParameterName = "@mainStockId";
                        idParam.Value = mainStockId;
                        command.Parameters.Add(idParam);

                        var dateParam = command.CreateParameter();
                        dateParam.ParameterName = "@updatedAt";
                        dateParam.Value = DateTime.Now;
                        command.Parameters.Add(dateParam);

                        await command.ExecuteNonQueryAsync();
                    }

                    // Step 3: Create transfer record
                    string referenceNumber = $"TRF-{DateTime.Now:yyyyMMddHHmmss}-{mainStockId}-{productId}";
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction.GetDbTransaction();
                        command.CommandText = @"
                INSERT INTO InventoryTransfers (
                    MainStockId, ProductId, Quantity, TransferDate, Notes, ReferenceNumber, TransferredBy
                ) VALUES (
                    @mainStockId, @productId, @quantity, @transferDate, @notes, @referenceNumber, @transferredBy
                )";

                        var mainStockIdParam = command.CreateParameter();
                        mainStockIdParam.ParameterName = "@mainStockId";
                        mainStockIdParam.Value = mainStockId;
                        command.Parameters.Add(mainStockIdParam);

                        var productIdParam = command.CreateParameter();
                        productIdParam.ParameterName = "@productId";
                        productIdParam.Value = productId;
                        command.Parameters.Add(productIdParam);

                        var quantityParam = command.CreateParameter();
                        quantityParam.ParameterName = "@quantity";
                        quantityParam.Value = quantity;
                        command.Parameters.Add(quantityParam);

                        var dateParam = command.CreateParameter();
                        dateParam.ParameterName = "@transferDate";
                        dateParam.Value = DateTime.Now;
                        command.Parameters.Add(dateParam);

                        var notesParam = command.CreateParameter();
                        notesParam.ParameterName = "@notes";
                        notesParam.Value = notes ?? "";
                        command.Parameters.Add(notesParam);

                        var refParam = command.CreateParameter();
                        refParam.ParameterName = "@referenceNumber";
                        refParam.Value = referenceNumber;
                        command.Parameters.Add(refParam);

                        var byParam = command.CreateParameter();
                        byParam.ParameterName = "@transferredBy";
                        byParam.Value = transferredBy;
                        command.Parameters.Add(byParam);

                        await command.ExecuteNonQueryAsync();
                    }

                    // Step 4: Update product - add inventory
                    decimal newCurrentStock = 0;
                    int newNumberOfBoxes = 0;
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction.GetDbTransaction();

                        if (isByBoxes)
                        {
                            command.CommandText = @"
                    UPDATE Products 
                    SET NumberOfBoxes = NumberOfBoxes + @quantity, 
                        UpdatedAt = @updatedAt
                    WHERE ProductId = @productId;
                    
                    SELECT CAST(CurrentStock AS DECIMAL(18,2)), NumberOfBoxes FROM Products WHERE ProductId = @productId;";

                            // For box transfers, we need to explicitly convert to int
                            var quantityParam = command.CreateParameter();
                            quantityParam.ParameterName = "@quantity";
                            quantityParam.Value = Convert.ToInt32(quantity);
                            command.Parameters.Add(quantityParam);
                        }
                        else
                        {
                            command.CommandText = @"
                    UPDATE Products 
                    SET CurrentStock = CurrentStock + @quantity, 
                        UpdatedAt = @updatedAt
                    WHERE ProductId = @productId;
                    
                    SELECT CAST(CurrentStock AS DECIMAL(18,2)), NumberOfBoxes FROM Products WHERE ProductId = @productId;";

                            var quantityParam = command.CreateParameter();
                            quantityParam.ParameterName = "@quantity";
                            quantityParam.Value = quantity;
                            command.Parameters.Add(quantityParam);
                        }

                        var idParam = command.CreateParameter();
                        idParam.ParameterName = "@productId";
                        idParam.Value = productId;
                        command.Parameters.Add(idParam);

                        var dateParam = command.CreateParameter();
                        dateParam.ParameterName = "@updatedAt";
                        dateParam.Value = DateTime.Now;
                        command.Parameters.Add(dateParam);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                // Make sure to use the correct reader methods
                                newCurrentStock = reader.GetDecimal(0);  // Use GetDecimal for CurrentStock
                                newNumberOfBoxes = reader.GetInt32(1);   // Use GetInt32 for NumberOfBoxes
                                Debug.WriteLine($"Updated product stock to {newCurrentStock} items, {newNumberOfBoxes} boxes");
                            }
                        }
                    }

                    // Step 5: Create inventory history record
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction.GetDbTransaction();
                        command.CommandText = @"
                INSERT INTO InventoryHistories (
                    ProductId, QuantityChange, NewQuantity, Type, Notes, Timestamp
                ) VALUES (
                    @productId, @quantityChange, @newQuantity, @type, @notes, @timestamp
                )";

                        var productIdParam = command.CreateParameter();
                        productIdParam.ParameterName = "@productId";
                        productIdParam.Value = productId;
                        command.Parameters.Add(productIdParam);

                        var quantityChangeParam = command.CreateParameter();
                        quantityChangeParam.ParameterName = "@quantityChange";
                        quantityChangeParam.Value = quantity;
                        command.Parameters.Add(quantityChangeParam);

                        var newQuantityParam = command.CreateParameter();
                        newQuantityParam.ParameterName = "@newQuantity";
                        // Choose the appropriate type based on transfer type
                        newQuantityParam.Value = isByBoxes ? (object)newNumberOfBoxes : (object)newCurrentStock;
                        command.Parameters.Add(newQuantityParam);

                        var typeParam = command.CreateParameter();
                        typeParam.ParameterName = "@type";
                        typeParam.Value = isByBoxes ? "Transfer-Box" : "Transfer-Item";
                        command.Parameters.Add(typeParam);

                        var notesParam = command.CreateParameter();
                        notesParam.ParameterName = "@notes";
                        notesParam.Value = isByBoxes
                            ? $"Box Transfer from MainStock ID: {mainStockId} - {quantity} boxes"
                            : $"Item Transfer from MainStock ID: {mainStockId} - {quantity} items";
                        command.Parameters.Add(notesParam);

                        var timestampParam = command.CreateParameter();
                        timestampParam.ParameterName = "@timestamp";
                        timestampParam.Value = DateTime.Now;
                        command.Parameters.Add(timestampParam);

                        await command.ExecuteNonQueryAsync();
                    }

                    // Commit the transaction
                    await transaction.CommitAsync();

                    // Publish events after transaction completes
                    var updatedMainStockDto = await GetMainStockDtoForEvent(mainStockId);
                    if (updatedMainStockDto != null)
                    {
                        _eventAggregator.Publish(new EntityChangedEvent<MainStockDTO>("Update", updatedMainStockDto));
                    }

                    // Convert decimal newCurrentStock to int32 for the event if needed
                    _eventAggregator.Publish(new ProductStockUpdatedEvent(productId, (int)newCurrentStock));

                    Debug.WriteLine($"Successfully transferred {quantity} {(isByBoxes ? "boxes" : "items")} from MainStock {mainStockId} to Product {productId}");
                    return true;
                }
                catch (Exception ex)
                {
                    // Ensure transaction is rolled back
                    await transaction.RollbackAsync();

                    // Log the detailed error
                    Debug.WriteLine($"Error in inventory transfer: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    }

                    // Re-throw with appropriate message
                    throw new InvalidOperationException($"Transfer failed: {ex.Message}", ex);
                }
            });
        }

        // Helper method to sync basic product data with MainStock
        private async Task SyncProductBasicData(MainStock mainStock, Product product)
        {
            if (product.ItemsPerBox != mainStock.ItemsPerBox ||
                product.PurchasePrice != mainStock.PurchasePrice ||
                product.SalePrice != mainStock.SalePrice ||
                product.WholesalePrice != mainStock.WholesalePrice ||
                product.BoxPurchasePrice != mainStock.BoxPurchasePrice ||
                product.BoxSalePrice != mainStock.BoxSalePrice ||
                product.BoxWholesalePrice != mainStock.BoxWholesalePrice)
            {
                // Use SQL to update basic data to avoid tracking issues
                string sql = @"
            UPDATE Products 
            SET ItemsPerBox = @itemsPerBox,
                PurchasePrice = @purchasePrice,
                SalePrice = @salePrice,
                WholesalePrice = @wholesalePrice,
                BoxPurchasePrice = @boxPurchasePrice,
                BoxSalePrice = @boxSalePrice,
                BoxWholesalePrice = @boxWholesalePrice,
                UpdatedAt = @updatedAt
            WHERE ProductId = @productId";

                var parameters = new[]
                {
            new Microsoft.Data.SqlClient.SqlParameter("@productId", product.ProductId),
            new Microsoft.Data.SqlClient.SqlParameter("@itemsPerBox", mainStock.ItemsPerBox),
            new Microsoft.Data.SqlClient.SqlParameter("@purchasePrice", mainStock.PurchasePrice),
            new Microsoft.Data.SqlClient.SqlParameter("@salePrice", mainStock.SalePrice),
            new Microsoft.Data.SqlClient.SqlParameter("@wholesalePrice", mainStock.WholesalePrice),
            new Microsoft.Data.SqlClient.SqlParameter("@boxPurchasePrice", mainStock.BoxPurchasePrice),
            new Microsoft.Data.SqlClient.SqlParameter("@boxSalePrice", mainStock.BoxSalePrice),
            new Microsoft.Data.SqlClient.SqlParameter("@boxWholesalePrice", mainStock.BoxWholesalePrice),
            new Microsoft.Data.SqlClient.SqlParameter("@updatedAt", DateTime.Now)
        };

                await _unitOfWork.Context.Database.ExecuteSqlRawAsync(sql, parameters);
            }
        }
        // Helper method to get MainStock data for event publishing
        private async Task<MainStockDTO> GetMainStockDtoForEvent(int mainStockId)
        {
            var mainStock = await _unitOfWork.Context.Set<MainStock>()
                .Include(m => m.Category)
                .Include(m => m.Supplier)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MainStockId == mainStockId);

            var dto = mainStock != null ? _mapper.Map<MainStockDTO>(mainStock) : null;

            // Ensure names are set
            if (mainStock != null && dto != null)
            {
                if (mainStock.Category != null)
                    dto.CategoryName = mainStock.Category.Name;
                if (mainStock.Supplier != null)
                    dto.SupplierName = mainStock.Supplier.Name;
            }

            return dto;
        }

        public override async Task<MainStockDTO> UpdateAsync(MainStockDTO dto)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    if (dto.UpdatedAt == null)
                        dto.UpdatedAt = DateTime.Now;

                    if (string.IsNullOrWhiteSpace(dto.BoxBarcode) && !string.IsNullOrWhiteSpace(dto.Barcode))
                        dto.BoxBarcode = $"BX{dto.Barcode}";

                    if (dto.ItemsPerBox <= 0)
                        dto.ItemsPerBox = 1;

                    if (string.IsNullOrEmpty(dto.CategoryName) && dto.CategoryId > 0)
                    {
                        var category = await _unitOfWork.Categories.GetByIdAsync(dto.CategoryId);
                        if (category != null)
                            dto.CategoryName = category.Name;
                    }

                    if (string.IsNullOrEmpty(dto.SupplierName) && dto.SupplierId.HasValue && dto.SupplierId.Value > 0)
                    {
                        var supplier = await _unitOfWork.Suppliers.GetByIdAsync(dto.SupplierId.Value);
                        if (supplier != null)
                            dto.SupplierName = supplier.Name;
                    }

                    var existingEntity = await _repository.GetByIdAsync(dto.MainStockId);
                    if (existingEntity != null)
                        _unitOfWork.DetachEntity(existingEntity);

                    var entity = _mapper.Map<MainStock>(dto);

                    if (dto.AutoSyncToProducts)
                    {
                        entity.CurrentStock = 0;
                        entity.NumberOfBoxes = 0;
                    }
                    else
                    {
                        entity.CurrentStock = dto.CurrentStock;
                        entity.NumberOfBoxes = dto.NumberOfBoxes;
                    }

                    entity.ItemsPerBox = dto.ItemsPerBox;

                    await _repository.UpdateAsync(entity);
                    await _unitOfWork.SaveChangesAsync();

                    var updatedEntity = await _repository.Query()
                        .Include(m => m.Category)
                        .Include(m => m.Supplier)
                        .FirstOrDefaultAsync(m => m.MainStockId == dto.MainStockId);

                    var resultDto = _mapper.Map<MainStockDTO>(updatedEntity ?? entity);

                    if (updatedEntity != null)
                    {
                        if (updatedEntity.Category != null)
                            resultDto.CategoryName = updatedEntity.Category.Name;
                        if (updatedEntity.Supplier != null)
                            resultDto.SupplierName = updatedEntity.Supplier.Name;
                    }
                    else
                    {
                        resultDto.CategoryName = dto.CategoryName;
                        resultDto.SupplierName = dto.SupplierName;
                    }

                    if (dto.AutoSyncToProducts)
                    {
                        var originalCurrentStock = dto.CurrentStock;
                        resultDto.CurrentStock = originalCurrentStock;
                        await AutoSyncToProductsAsync(resultDto);
                        resultDto.CurrentStock = 0;
                    }

                    _eventAggregator.Publish(new EntityChangedEvent<MainStockDTO>("Update", resultDto));

                    var linkedProducts = await _unitOfWork.Products
                        .Query()
                        .Include(p => p.Category)
                        .Include(p => p.Supplier)
                        .Where(p => p.MainStockId == dto.MainStockId)
                        .ToListAsync();

                    if (linkedProducts.Any())
                    {
                        foreach (var product in linkedProducts)
                        {
                            product.PurchasePrice = dto.PurchasePrice;
                            product.SalePrice = dto.SalePrice;
                            product.BoxPurchasePrice = dto.BoxPurchasePrice;
                            product.BoxSalePrice = dto.BoxSalePrice;
                            product.ItemsPerBox = dto.ItemsPerBox;
                            product.UpdatedAt = DateTime.Now;
                            product.WholesalePrice = dto.WholesalePrice;
                            product.BoxWholesalePrice = dto.BoxWholesalePrice;

                            await _unitOfWork.Products.UpdateAsync(product);

                            var productDto = _mapper.Map<ProductDTO>(product);
                            if (product.Category != null)
                                productDto.CategoryName = product.Category.Name;
                            if (product.Supplier != null)
                                productDto.SupplierName = product.Supplier.Name;

                            _eventAggregator.Publish(new EntityChangedEvent<ProductDTO>("Update", productDto));
                        }
                        await _unitOfWork.SaveChangesAsync();
                    }

                    return resultDto;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error updating MainStock: {ex.Message}");
                    throw;
                }
            });
        }
        public async Task<IEnumerable<MainStockDTO>> SearchAsync(string searchTerm)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var query = _repository.Query()
                    .Include(p => p.Category)
                    .Include(p => p.Supplier)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = searchTerm.ToLower();
                    query = query.Where(p =>
                        p.Name.ToLower().Contains(searchTerm) ||
                        p.Barcode.ToLower().Contains(searchTerm) ||
                        p.BoxBarcode.ToLower().Contains(searchTerm) ||
                        (p.Category != null && p.Category.Name.ToLower().Contains(searchTerm)) ||
                        (p.Supplier != null && p.Supplier.Name.ToLower().Contains(searchTerm)) ||
                        (p.Description != null && p.Description.ToLower().Contains(searchTerm))
                    );
                }

                var products = await query.ToListAsync();
                var dtos = _mapper.Map<IEnumerable<MainStockDTO>>(products);

                // Manually set the names since they don't exist in entities
                foreach (var dto in dtos)
                {
                    var product = products.FirstOrDefault(p => p.MainStockId == dto.MainStockId);
                    if (product != null)
                    {
                        if (product.Category != null)
                        {
                            dto.CategoryName = product.Category.Name;
                        }

                        if (product.Supplier != null)
                        {
                            dto.SupplierName = product.Supplier.Name;
                        }
                    }
                }

                return dtos;
            });
        }
    }
}