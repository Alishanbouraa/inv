using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Mappings;
using QuickTechSystems.Application.Services;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.Domain.Interfaces.Repositories;
using QuickTechSystems.Infrastructure.Data;
using QuickTechSystems.Infrastructure.Repositories;
using QuickTechSystems.Infrastructure.Services;
using QuickTechSystems.ViewModels;
using QuickTechSystems.Views;
using QuickTechSystems.WPF.ViewModels;
using QuickTechSystems.WPF.Views;
using QuickTechSystems.WPF.Services;
using System;
using System.IO;
using System.Windows;
using QuickTechSystems.Application.Helpers;
using QuickTechSystems.Helpers;
using Microsoft.EntityFrameworkCore.Infrastructure;
using QuickTechSystems.Application.Interfaces;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Markup;

namespace QuickTechSystems.WPF
{
    public partial class App : System.Windows.Application
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly IEventAggregator _eventAggregator;
        public IServiceProvider ServiceProvider => _serviceProvider;

        public App()
        {
            InitializeComponent();

            System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            _configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            _eventAggregator = _serviceProvider.GetRequiredService<IEventAggregator>();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IConfiguration>(_configuration);
            services.AddSingleton<IEventAggregator, EventAggregator>();
            services.AddAutoMapper(typeof(MappingProfile));
            services.AddSingleton<IWindowService, WindowService>();
            services.AddSingleton<LanguageManager>();
            services.AddScoped<IPrinterService, PrinterService>();
            services.AddScoped<ICustomerPrintService, QuickTechSystems.WPF.Services.CustomerPrintService>();

            services.AddDbContextPool<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    _configuration.GetConnectionString("DefaultConnection"),
                    b => b.MigrationsAssembly("QuickTechSystems.Infrastructure"))
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors());

            services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(provider =>
                new PooledDbContextFactory<ApplicationDbContext>(
                    new DbContextOptionsBuilder<ApplicationDbContext>()
                        .UseSqlServer(_configuration.GetConnectionString("DefaultConnection"),
                            b => b.MigrationsAssembly("QuickTechSystems.Infrastructure"))
                        .EnableSensitiveDataLogging()
                        .EnableDetailedErrors()
                        .Options));

            services.AddScoped<IDbContextScopeService, DbContextScopeService>();

            services.AddTransient<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IBackupService, BackupService>();
           
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IBarcodeService, BarcodeService>();
            services.AddScoped<IBusinessSettingsService, BusinessSettingsService>();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<ICustomerService, CustomerService>();
            services.AddScoped<IDrawerService, DrawerService>();
            services.AddScoped<IEmployeeService, EmployeeService>();
            services.AddScoped<IExpenseService, ExpenseService>();
            services.AddScoped<ISupplierService, SupplierService>();
            services.AddScoped<ISystemPreferencesService, SystemPreferencesService>();
           
            services.AddScoped<IRestaurantTableService, RestaurantTableService>();
            services.AddScoped<TableManagementViewModel>();
            services.AddTransient<TableManagementView>();
            services.AddScoped<ISupplierInvoiceService, SupplierInvoiceService>();

            services.AddScoped<IGenericRepository<SupplierInvoice>>(provider =>
       provider.GetRequiredService<IUnitOfWork>().SupplierInvoices);
            services.AddScoped<IGenericRepository<SupplierInvoiceDetail>>(provider =>
                provider.GetRequiredService<IUnitOfWork>().SupplierInvoiceDetails);

            services.AddScoped<SplashScreenViewModel>();

            services.AddSingleton<IImagePathService, ImagePathService>();
            services.AddTransient<SplashScreenView>();

            services.AddScoped<MainViewModel>();
            services.AddScoped<LoginViewModel>();
            services.AddScoped<DashboardViewModel>();
            services.AddScoped<CategoryViewModel>();
            services.AddScoped<CustomerViewModel>();
            services.AddScoped<DrawerViewModel>();
            services.AddScoped<EmployeeViewModel>();
            services.AddScoped<ExpenseViewModel>();
             services.AddScoped<SettingsViewModel>();
            services.AddScoped<SupplierViewModel>();
            services.AddScoped<SystemPreferencesViewModel>();
             services.AddTransient<SupplierInvoiceViewModel>();

            services.AddTransient<MainWindow>();
            services.AddTransient<LoginView>();
            services.AddTransient<CategoryView>();
            services.AddTransient<CustomerView>();
            services.AddTransient<DrawerView>();
            services.AddTransient<EmployeeView>();
            services.AddTransient<ExpenseView>();
            services.AddTransient<SettingsView>();
            services.AddTransient<SupplierView>();
            services.AddTransient<SystemPreferencesView>();

            services.AddTransient<QuantityDialog>();
            }

        protected override async void OnStartup(StartupEventArgs e)
        {
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(
                    XmlLanguage.GetLanguage(CultureInfo.InvariantCulture.IetfLanguageTag)));

            base.OnStartup(e);

            try
            {
                var splashViewModel = _serviceProvider.GetRequiredService<SplashScreenViewModel>();
                var splashView = _serviceProvider.GetRequiredService<SplashScreenView>();
                splashView.Show();

                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                Directory.CreateDirectory(logPath);
                File.WriteAllText(Path.Combine(logPath, "startup.log"), $"Application starting at {DateTime.Now}...");

                splashViewModel.UpdateStatus("Initializing database...");

                await Task.Run(async () =>
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var businessSettingsService = scope.ServiceProvider.GetRequiredService<IBusinessSettingsService>();
                        var systemPreferencesService = scope.ServiceProvider.GetRequiredService<ISystemPreferencesService>();
                        var languageManager = scope.ServiceProvider.GetRequiredService<LanguageManager>();
                        var eventAggregator = scope.ServiceProvider.GetRequiredService<IEventAggregator>();

                        try
                        {
                            splashViewModel.UpdateStatus("Ensuring database exists...");
                            await context.Database.EnsureCreatedAsync();

                            splashViewModel.UpdateStatus("Initializing database...");
                            DatabaseInitializer.Initialize(context);

                            splashViewModel.UpdateStatus("Creating default admin account...");
                            DatabaseInitializer.SeedDefaultAdmin(context);

                            splashViewModel.UpdateStatus("Setting up system preferences...");
                            const string userId = "default";
                            var hasPreferences = await systemPreferencesService.GetPreferenceValueAsync(userId, "Initialized", "false");
                            if (hasPreferences != "true")
                            {
                                await systemPreferencesService.InitializeUserPreferencesAsync(userId);
                                await systemPreferencesService.SavePreferenceAsync(userId, "Initialized", "true");
                            }
                        }
                        catch (Exception dbEx)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show(
                                    $"Database initialization error: {dbEx.Message}\n\nPlease ensure SQL Server is installed and accessible with the provided credentials.",
                                    "Database Error",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                                Shutdown();
                            });
                            return;
                        }

                        try
                        {
                            splashViewModel.UpdateStatus("Loading business settings...");
                            var rateSetting = await businessSettingsService.GetByKeyAsync("ExchangeRate");
                            if (rateSetting != null && decimal.TryParse(rateSetting.Value, out decimal rate))
                            {
                                CurrencyHelper.UpdateExchangeRate(rate);
                            }

                            string restaurantModeStr = "false";
                            bool isRestaurantMode = false;

                            try
                            {
                                splashViewModel.UpdateStatus("Loading user preferences...");
                                restaurantModeStr = await systemPreferencesService.GetPreferenceValueAsync("default", "RestaurantMode", "false");
                                isRestaurantMode = bool.Parse(restaurantModeStr);
                                Debug.WriteLine($"Loaded RestaurantMode preference: {isRestaurantMode}");
                            }
                            catch (Exception prefEx)
                            {
                                Debug.WriteLine($"Error loading restaurant mode preference: {prefEx.Message}");
                            }

                            try
                            {
                                splashViewModel.UpdateStatus("Setting language preferences...");
                                var defaultLanguage = await systemPreferencesService.GetPreferenceValueAsync("default", "Language", "en-US");

                                await Dispatcher.Invoke(async () =>
                                {
                                    await languageManager.SetLanguage(defaultLanguage);
                                });
                            }
                            catch (Exception langEx)
                            {
                                Debug.WriteLine($"Error setting language: {langEx.Message}");
                            }

                            if (isRestaurantMode)
                            {
                                Properties["RestaurantModeEnabled"] = true;
                            }
                        }
                        catch (Exception settingsEx)
                        {
                            File.AppendAllText(Path.Combine(logPath, "startup.log"), $"\nSettings error: {settingsEx.Message}");
                            Debug.WriteLine($"Settings error: {settingsEx.Message}");
                        }
                    }

                    splashViewModel.UpdateStatus("Launching application...");

                    await Task.Delay(800);
                });

                Dispatcher.Invoke(() =>
                {
                    var loginView = _serviceProvider.GetRequiredService<LoginView>();
                    loginView.Show();
                    splashView.Close();
                });
            }
            catch (Exception ex)
            {
                var errorMessage = $"An error occurred while starting the application: {ex.Message}\n\n" +
                                  "Please ensure:\n" +
                                  "1. SQL Server is installed and accessible\n" +
                                  "2. .NET 8.0 Desktop Runtime is installed\n" +
                                  "3. You have necessary permissions to access the application folder";

                MessageBox.Show(errorMessage, "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);

                try
                {
                    var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                    File.AppendAllText(Path.Combine(logPath, "error.log"),
                        $"\n[{DateTime.Now}] Fatal startup error:\n{ex}\n");
                }
                catch
                {
                }

                Shutdown();
            }
        }

        public void ApplyRestaurantModeSetting()
        {
            if (Properties.Contains("RestaurantModeEnabled") && (bool)Properties["RestaurantModeEnabled"])
            {
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        _eventAggregator.Publish(new ApplicationModeChangedEvent(true));
                        Debug.WriteLine("Published ApplicationModeChangedEvent: true");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error publishing restaurant mode event: {ex.Message}");
                    }
                });
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}