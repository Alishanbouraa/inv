// Path: QuickTechSystems.Infrastructure.Data/ApplicationDbContext.cs
using Microsoft.EntityFrameworkCore;
using QuickTechSystems.Domain.Entities;
using QuickTechSystems.Infrastructure.Data.Configurations;
using System.Reflection;

namespace QuickTechSystems.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Employee> Employees => Set<Employee>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Transaction> Transactions => Set<Transaction>();
        public DbSet<TransactionDetail> TransactionDetails => Set<TransactionDetail>();
        public DbSet<InventoryHistory> InventoryHistories { get; set; }
        public DbSet<BusinessSetting> BusinessSettings => Set<BusinessSetting>();
        public DbSet<SystemPreference> SystemPreferences => Set<SystemPreference>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<SupplierTransaction> SupplierTransactions => Set<SupplierTransaction>();
        public DbSet<Expense> Expenses => Set<Expense>();
        public DbSet<Drawer> Drawers => Set<Drawer>();
        public DbSet<DrawerHistoryEntry> DrawerHistory => Set<DrawerHistoryEntry>();
        public DbSet<DrawerTransaction> DrawerTransactions => Set<DrawerTransaction>();
       
        public DbSet<EmployeeSalaryTransaction> EmployeeSalaryTransactions => Set<EmployeeSalaryTransaction>();
        public DbSet<RestaurantTable> RestaurantTables => Set<RestaurantTable>();
        public DbSet<SupplierInvoice> SupplierInvoices { get; set; }
        public DbSet<SupplierInvoiceDetail> SupplierInvoiceDetails { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
             modelBuilder.ApplyConfiguration(new SupplierInvoiceConfiguration());
            modelBuilder.ApplyConfiguration(new SupplierInvoiceDetailConfiguration());
            modelBuilder.ApplyConfiguration(new InventoryHistoryConfiguration());
         
        }

        public DbContextOptions<ApplicationDbContext> GetConfiguration()
        {
            return (DbContextOptions<ApplicationDbContext>)this.GetType()
                .GetProperty("ContextOptions")
                .GetValue(this);
        }
    }
}