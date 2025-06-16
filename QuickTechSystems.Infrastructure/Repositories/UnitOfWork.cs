using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using QuickTechSystems.Domain.Entities;
using QuickTechSystems.Domain.Interfaces.Repositories;
using QuickTechSystems.Infrastructure.Data;
using System;
using System.Threading.Tasks;

namespace QuickTechSystems.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork, IDisposable
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private ApplicationDbContext _context;
        private bool _disposed;

        private IGenericRepository<Product>? _products;
    
        private IGenericRepository<Category>? _categories;
        private IGenericRepository<Customer>? _customers;
        private IGenericRepository<Transaction>? _transactions;
        private IGenericRepository<BusinessSetting>? _businessSettings;
        private IGenericRepository<SystemPreference>? _systemPreferences;
        private IGenericRepository<Supplier>? _suppliers;
        private IGenericRepository<Expense>? _expenses;
        private IGenericRepository<Employee>? _employees;
        private IGenericRepository<Drawer>? _drawers;
        private IGenericRepository<InventoryHistory>? _inventoryHistories;
        
        private IGenericRepository<RestaurantTable>? _restaurantTables;
        private IGenericRepository<SupplierInvoice>? _supplierInvoices;
        private IGenericRepository<SupplierInvoiceDetail>? _supplierInvoiceDetails;

       

        public IGenericRepository<InventoryHistory> InventoryHistories =>
            _inventoryHistories ??= new GenericRepository<InventoryHistory>(_context, _contextFactory);

        public IGenericRepository<SupplierInvoice> SupplierInvoices =>
            _supplierInvoices ??= new GenericRepository<SupplierInvoice>(_context, _contextFactory);

        public IGenericRepository<SupplierInvoiceDetail> SupplierInvoiceDetails =>
            _supplierInvoiceDetails ??= new GenericRepository<SupplierInvoiceDetail>(_context, _contextFactory);

        public IGenericRepository<RestaurantTable> RestaurantTables =>
            _restaurantTables ??= new GenericRepository<RestaurantTable>(_context, _contextFactory);

        public UnitOfWork(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _context = _contextFactory.CreateDbContext();
        }

        public void DetachEntity<T>(T entity) where T : class
        {
            if (entity == null) return;

            var entry = _context.Entry(entity);
            if (entry != null)
            {
                entry.State = EntityState.Detached;
            }
        }

        public void DetachAllEntities()
        {
            var entries = _context.ChangeTracker.Entries().ToList();
            foreach (var entry in entries)
            {
                entry.State = EntityState.Detached;
            }
        }

      

        public IGenericRepository<Employee> Employees =>
            _employees ??= new GenericRepository<Employee>(_context, _contextFactory);

        public IGenericRepository<Product> Products =>
            _products ??= new GenericRepository<Product>(_context, _contextFactory);

        public IGenericRepository<Category> Categories =>
            _categories ??= new GenericRepository<Category>(_context, _contextFactory);

        public IGenericRepository<Customer> Customers =>
            _customers ??= new GenericRepository<Customer>(_context, _contextFactory);

        public IGenericRepository<Transaction> Transactions =>
            _transactions ??= new GenericRepository<Transaction>(_context, _contextFactory);

        public IGenericRepository<BusinessSetting> BusinessSettings =>
            _businessSettings ??= new GenericRepository<BusinessSetting>(_context, _contextFactory);

        public IGenericRepository<SystemPreference> SystemPreferences =>
            _systemPreferences ??= new GenericRepository<SystemPreference>(_context, _contextFactory);

        public IGenericRepository<Expense> Expenses =>
            _expenses ??= new GenericRepository<Expense>(_context, _contextFactory);

        public IGenericRepository<Drawer> Drawers =>
            _drawers ??= new GenericRepository<Drawer>(_context, _contextFactory);

        public IGenericRepository<Supplier> Suppliers =>
            _suppliers ??= new GenericRepository<Supplier>(_context, _contextFactory);

      
        public DbContext Context => _context;

        public async Task<int> SaveChangesAsync()
        {
            try
            {
                return await _context.SaveChangesAsync();
            }
            catch (ObjectDisposedException)
            {
                _context = _contextFactory.CreateDbContext();
                ResetRepositories();
                return await _context.SaveChangesAsync();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("disposed") || ex.Message.Contains("second operation"))
            {
                _context = _contextFactory.CreateDbContext();
                ResetRepositories();
                return await _context.SaveChangesAsync();
            }
        }

        private void ResetRepositories()
        {
            _restaurantTables = null;
            _products = null;
            _categories = null;
            _customers = null;
            _transactions = null;
            _businessSettings = null;
            _systemPreferences = null;
            _suppliers = null;
            _expenses = null;
            _employees = null;
            _drawers = null;
            _supplierInvoices = null;
            _supplierInvoiceDetails = null;
            _inventoryHistories = null;
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync()
        {
            try
            {
                return await _context.Database.BeginTransactionAsync();
            }
            catch (ObjectDisposedException)
            {
                _context = _contextFactory.CreateDbContext();
                ResetRepositories();
                return await _context.Database.BeginTransactionAsync();
            }
        }

        public IGenericRepository<T> GetRepository<T>() where T : class
        {
            var propertyName = typeof(T).Name + "s";
            var property = GetType().GetProperties()
                .FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));

            if (property != null)
            {
                return (IGenericRepository<T>)property.GetValue(this);
            }

            return new GenericRepository<T>(_context, _contextFactory);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _context.Dispose();
            }
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}