using System.Diagnostics;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Interfaces;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.Domain.Entities;
using QuickTechSystems.Domain.Interfaces.Repositories;

namespace QuickTechSystems.Application.Services
{
    public class ProductService : BaseService<Product, ProductDTO>, IProductService
    {
        public ProductService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IEventAggregator eventAggregator,
            IDbContextScopeService dbContextScopeService)
            : base(unitOfWork, mapper, eventAggregator, dbContextScopeService)
        {
        }

        public override async Task<ProductDTO> UpdateAsync(ProductDTO dto)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    if (dto.UpdatedAt == null)
                        dto.UpdatedAt = DateTime.Now;

                    var existingEntity = await _repository.GetByIdAsync(dto.ProductId);
                    if (existingEntity != null)
                    {
                        _unitOfWork.DetachEntity(existingEntity);
                    }

                    var entity = _mapper.Map<Product>(dto);

                    await _repository.UpdateAsync(entity);
                    await _unitOfWork.SaveChangesAsync();

                    var updatedEntity = await _repository.Query()
                        .Include(p => p.Category)
                        .Include(p => p.Supplier)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.ProductId == dto.ProductId);

                    var resultDto = _mapper.Map<ProductDTO>(updatedEntity ?? entity);

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

                    _eventAggregator.Publish(new EntityChangedEvent<ProductDTO>("Update", resultDto));

                    return resultDto;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error updating Product: {ex.Message}");
                    throw;
                }
            });
        }
        public async Task<IEnumerable<ProductDTO>> GetByCategoryAsync(int categoryId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var products = await _repository.Query()
                    .Include(p => p.Category)
                    .Where(p => p.CategoryId == categoryId)
                    .ToListAsync();
                return _mapper.Map<IEnumerable<ProductDTO>>(products);
            });
        }

        public async Task<IEnumerable<ProductDTO>> GetLowStockProductsAsync()
        {
            return await GetLowStockProductsAsync(null);
        }

        public async Task<IEnumerable<ProductDTO>> GetLowStockProductsAsync(int? customThreshold = null)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var products = await _repository.Query()
                    .Include(p => p.Category)
                    .Include(p => p.Supplier)
                    .Where(p => p.CurrentStock <= (customThreshold ?? p.MinimumStock))
                    .ToListAsync();

                return _mapper.Map<IEnumerable<ProductDTO>>(products);
            });
        }

        public async Task<List<ProductDTO>> CreateBatchAsync(List<ProductDTO> products, IProgress<string>? progress = null)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                Debug.WriteLine($"Notice: CreateBatchAsync called in ProductService, which is deprecated. Use MainStockService instead.");

                var savedProducts = new List<ProductDTO>();

                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    for (int i = 0; i < products.Count; i++)
                    {
                        var product = products[i];
                        progress?.Report($"Saving product {i + 1} of {products.Count}: {product.Name}");

                        if (product.CreatedAt == default)
                            product.CreatedAt = DateTime.Now;

                        var entity = _mapper.Map<Product>(product);
                        var result = await _repository.AddAsync(entity);
                        var resultDto = _mapper.Map<ProductDTO>(result);
                        savedProducts.Add(resultDto);
                    }

                    await _unitOfWork.SaveChangesAsync();
                    await transaction.CommitAsync();

                    foreach (var savedProduct in savedProducts)
                    {
                        _eventAggregator.Publish(new EntityChangedEvent<ProductDTO>("Create", savedProduct));
                    }

                    return savedProducts;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in batch save: {ex.Message}");
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task<ProductDTO> FindProductByBarcodeAsync(string barcode, int excludeProductId = 0)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var query = _repository.Query()
                    .AsNoTracking()
                    .Include(p => p.Category)
                    .Include(p => p.Supplier)
                    .Where(p => p.Barcode == barcode);

                if (excludeProductId > 0)
                {
                    query = query.Where(p => p.ProductId != excludeProductId);
                }

                var product = await query.FirstOrDefaultAsync();
                var dto = _mapper.Map<ProductDTO>(product);

                if (product != null)
                {
                    if (product.Category != null)
                        dto.CategoryName = product.Category.Name;
                    if (product.Supplier != null)
                        dto.SupplierName = product.Supplier.Name;
                }

                return dto;
            });
        }

        public async Task<bool> UpdateStockAsync(int productId, decimal quantity)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    Debug.WriteLine($"Updating stock for product {productId} by {quantity}");

                    var product = await _repository.GetByIdAsync(productId);
                    if (product == null)
                    {
                        Debug.WriteLine($"Product {productId} not found for stock update");
                        return false;
                    }

                    decimal oldStock = product.CurrentStock;

                    product.CurrentStock = decimal.Add(product.CurrentStock, quantity);
                    product.UpdatedAt = DateTime.Now;

                    await _repository.UpdateAsync(product);
                    await _unitOfWork.SaveChangesAsync();

                    Debug.WriteLine($"Stock updated for product {productId}: {oldStock} → {product.CurrentStock}");

                    var productDto = _mapper.Map<ProductDTO>(product);
                    _eventAggregator.Publish(new EntityChangedEvent<ProductDTO>("Update", productDto));
                    _eventAggregator.Publish(new ProductStockUpdatedEvent(productId, product.CurrentStock));

                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error updating product stock: {ex.Message}");
                    throw;
                }
            });
        }

        public async Task<ProductDTO?> GetByBarcodeAsync(string barcode)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var product = await _repository.Query()
                    .Include(p => p.Category)
                    .FirstOrDefaultAsync(p => p.Barcode == barcode);
                return _mapper.Map<ProductDTO>(product);
            });
        }

        public async Task<bool> ReceiveInventoryAsync(int productId, decimal quantity, string source, string reference)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    var product = await _repository.GetByIdAsync(productId);
                    if (product == null)
                    {
                        Debug.WriteLine($"Product {productId} not found for inventory receipt");
                        return false;
                    }

                    decimal oldStock = product.CurrentStock;
                    product.CurrentStock += quantity;
                    product.UpdatedAt = DateTime.Now;

                    await _repository.UpdateAsync(product);

                    var inventoryHistory = new InventoryHistory
                    {
                        ProductId = productId,
                        QuantityChange = quantity,
                        NewQuantity = product.CurrentStock,
                        Type = "Receive",
                        Notes = $"Received from {source}: {reference}",
                        Timestamp = DateTime.Now
                    };

                    await _unitOfWork.InventoryHistories.AddAsync(inventoryHistory);
                    await _unitOfWork.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var productDto = _mapper.Map<ProductDTO>(product);
                    _eventAggregator.Publish(new EntityChangedEvent<ProductDTO>("Update", productDto));
                    _eventAggregator.Publish(new ProductStockUpdatedEvent(productId, product.CurrentStock));

                    Debug.WriteLine($"Inventory received for product {productId}: {quantity} units, new stock: {product.CurrentStock}");
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Debug.WriteLine($"Error receiving inventory: {ex.Message}");
                    throw;
                }
            });
        }

        public override async Task<IEnumerable<ProductDTO>> GetAllAsync()
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    Debug.WriteLine("ProductService: Performing complete refresh from database");

                    var products = await _repository.Query()
                        .AsNoTracking()
                        .Include(p => p.Category)
                        .Include(p => p.Supplier)
                        .Include(p => p.MainStock)
                        .ToListAsync();

                    var productDtos = _mapper.Map<IEnumerable<ProductDTO>>(products);

                    foreach (var productDto in productDtos)
                    {
                        if (productDto.MainStockId.HasValue)
                        {
                            var mainStock = await _unitOfWork.MainStocks
                                .Query()
                                .AsNoTracking()
                                .FirstOrDefaultAsync(m => m.MainStockId == productDto.MainStockId.Value);

                            if (mainStock != null)
                            {
                                if (Math.Abs(productDto.PurchasePrice - mainStock.PurchasePrice) > 0.001m)
                                {
                                    productDto.PurchasePrice = mainStock.PurchasePrice;
                                }

                                if (Math.Abs(productDto.SalePrice - mainStock.SalePrice) > 0.001m)
                                {
                                    productDto.SalePrice = mainStock.SalePrice;
                                }

                                productDto.BoxPurchasePrice = mainStock.BoxPurchasePrice;
                                productDto.BoxSalePrice = mainStock.BoxSalePrice;
                                productDto.ItemsPerBox = mainStock.ItemsPerBox;
                            }
                        }
                    }

                    return productDtos;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in ProductService.GetAllAsync: {ex}");
                    throw;
                }
            });
        }

        public async Task SynchronizeWithMainStockAsync(int productId)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    var product = await _repository.GetByIdAsync(productId);
                    if (product == null || !product.MainStockId.HasValue)
                    {
                        return;
                    }

                    var mainStock = await _unitOfWork.MainStocks.GetByIdAsync(product.MainStockId.Value);
                    if (mainStock == null)
                    {
                        return;
                    }

                    bool updated = false;

                    if (Math.Abs(product.PurchasePrice - mainStock.PurchasePrice) > 0.001m)
                    {
                        product.PurchasePrice = mainStock.PurchasePrice;
                        updated = true;
                    }

                    if (Math.Abs(product.WholesalePrice - mainStock.WholesalePrice) > 0.001m)
                    {
                        product.WholesalePrice = mainStock.WholesalePrice;
                        updated = true;
                    }

                    if (Math.Abs(product.SalePrice - mainStock.SalePrice) > 0.001m)
                    {
                        product.SalePrice = mainStock.SalePrice;
                        updated = true;
                    }

                    if (Math.Abs(product.BoxPurchasePrice - mainStock.BoxPurchasePrice) > 0.001m)
                    {
                        product.BoxPurchasePrice = mainStock.BoxPurchasePrice;
                        updated = true;
                    }

                    if (Math.Abs(product.BoxWholesalePrice - mainStock.BoxWholesalePrice) > 0.001m)
                    {
                        product.BoxWholesalePrice = mainStock.BoxWholesalePrice;
                        updated = true;
                    }

                    if (Math.Abs(product.BoxSalePrice - mainStock.BoxSalePrice) > 0.001m)
                    {
                        product.BoxSalePrice = mainStock.BoxSalePrice;
                        updated = true;
                    }

                    if (product.ItemsPerBox != mainStock.ItemsPerBox)
                    {
                        product.ItemsPerBox = mainStock.ItemsPerBox;
                        updated = true;
                    }

                    if (updated)
                    {
                        product.UpdatedAt = DateTime.Now;
                        await _repository.UpdateAsync(product);
                        await _unitOfWork.SaveChangesAsync();

                        var productDto = _mapper.Map<ProductDTO>(product);
                        _eventAggregator.Publish(new EntityChangedEvent<ProductDTO>("Update", productDto));

                        Debug.WriteLine($"ProductService: Synchronized product {productId} with MainStock {mainStock.MainStockId}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in SynchronizeWithMainStockAsync: {ex}");
                }
            });
        }
    }
}