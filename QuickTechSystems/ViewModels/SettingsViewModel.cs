using Microsoft.VisualBasic;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Helpers;
using QuickTechSystems.Helpers;
using QuickTechSystems.WPF;
using System.Windows.Forms;
using Microsoft.Win32;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace QuickTechSystems.WPF.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly LanguageManager _languageManager;
        private readonly IBusinessSettingsService _businessSettingsService;
        private readonly ISystemPreferencesService _systemPreferencesService;
        private readonly IBackupService _backupService;
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
        private bool _isDisposed;

        private string _selectedLanguage;
        private ObservableCollection<BusinessSettingDTO> _businessSettings;
        private BusinessSettingDTO? _selectedBusinessSetting;
        private string _selectedGroup = "All";
        private bool _isEditing;
        private Action<EntityChangedEvent<BusinessSettingDTO>> _settingChangedHandler;
        private decimal _exchangeRate;
        private bool _isLoading;
        private string _errorMessage;
        private byte[] _logoImage;

        // System Preferences Properties
        private bool _autoBackupEnabled;
        private string _selectedDay = "Monday";
        private string _backupTime = "12:00";
        private string _backupPath;
        private string _lastBackupStatus;

        private string _currentTheme = "Light";
        private string _currentLanguage = "en-US";
        private bool _soundEffectsEnabled = true;
        private bool _notificationsEnabled = true;
        private string _dateFormat = "MM/dd/yyyy";
        private string _timeFormat = "HH:mm:ss";
        private bool _isRestaurantMode;

        public SettingsViewModel(
            IBusinessSettingsService businessSettingsService,
            ISystemPreferencesService systemPreferencesService,
            IBackupService backupService,
            LanguageManager languageManager,
            IEventAggregator eventAggregator) : base(eventAggregator)
        {
            _businessSettingsService = businessSettingsService;
            _systemPreferencesService = systemPreferencesService;
            _backupService = backupService;
            _languageManager = languageManager;
            _settingChangedHandler = HandleSettingChanged;
            _businessSettings = new ObservableCollection<BusinessSettingDTO>();
            _errorMessage = string.Empty;

            InitializeCommands();
            _ = LoadDataAsync();
        }
        public void UpdateBusinessSettingDirectEdit(BusinessSettingDTO setting)
        {
            try
            {
                // Save the setting immediately when edited in-grid
                SaveBusinessSettingCommand.Execute(setting);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating setting: {ex.Message}");
            }
        }
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool IsRestaurantMode
        {
            get => _isRestaurantMode;
            set
            {
                if (SetProperty(ref _isRestaurantMode, value))
                {
                    _ = SavePreferenceAsync("RestaurantMode", value.ToString());
                    _eventAggregator.Publish(new ApplicationModeChangedEvent(value));
                }
            }
        }

        public ObservableCollection<LanguageInfo> AvailableLanguages => LanguageManager.AvailableLanguages;

        public decimal ExchangeRate
        {
            get => _exchangeRate;
            set => SetProperty(ref _exchangeRate, value);
        }

        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (SetProperty(ref _selectedLanguage, value))
                {
                    _ = _languageManager.SetLanguage(value);
                }
            }
        }

        public byte[] LogoImage
        {
            get => _logoImage;
            set => SetProperty(ref _logoImage, value);
        }

        public ObservableCollection<BusinessSettingDTO> BusinessSettings
        {
            get => _businessSettings;
            set => SetProperty(ref _businessSettings, value);
        }

        public BusinessSettingDTO? SelectedBusinessSetting
        {
            get => _selectedBusinessSetting;
            set
            {
                SetProperty(ref _selectedBusinessSetting, value);
                IsEditing = value != null;
            }
        }

        public string SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                SetProperty(ref _selectedGroup, value);
                _ = LoadSettingsByGroupAsync();
            }
        }

        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }

        public string CurrentTheme
        {
            get => _currentTheme;
            set
            {
                if (SetProperty(ref _currentTheme, value))
                {
                    _ = SavePreferenceAsync("Theme", value);
                }
            }
        }

        public string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (SetProperty(ref _currentLanguage, value))
                {
                    _ = SavePreferenceAsync("Language", value);
                }
            }
        }

        public bool SoundEffectsEnabled
        {
            get => _soundEffectsEnabled;
            set
            {
                if (SetProperty(ref _soundEffectsEnabled, value))
                {
                    _ = SavePreferenceAsync("SoundEffects", value.ToString());
                }
            }
        }

        public bool NotificationsEnabled
        {
            get => _notificationsEnabled;
            set
            {
                if (SetProperty(ref _notificationsEnabled, value))
                {
                    _ = SavePreferenceAsync("EnableNotifications", value.ToString());
                }
            }
        }

        public string DateFormat
        {
            get => _dateFormat;
            set
            {
                if (SetProperty(ref _dateFormat, value))
                {
                    _ = SavePreferenceAsync("DateFormat", value);
                }
            }
        }

        public string TimeFormat
        {
            get => _timeFormat;
            set
            {
                if (SetProperty(ref _timeFormat, value))
                {
                    _ = SavePreferenceAsync("TimeFormat", value);
                }
            }
        }

        public bool AutoBackupEnabled
        {
            get => _autoBackupEnabled;
            set => SetProperty(ref _autoBackupEnabled, value);
        }

        public string SelectedDay
        {
            get => _selectedDay;
            set => SetProperty(ref _selectedDay, value);
        }

        public string BackupTime
        {
            get => _backupTime;
            set => SetProperty(ref _backupTime, value);
        }

        public string BackupPath
        {
            get => _backupPath;
            set => SetProperty(ref _backupPath, value);
        }

        public string LastBackupStatus
        {
            get => _lastBackupStatus;
            set => SetProperty(ref _lastBackupStatus, value);
        }

        public ObservableCollection<string> DaysOfWeek { get; } = new(
            Enum.GetNames(typeof(DayOfWeek)));

        public ObservableCollection<string> AvailableThemes { get; } = new()
        {
           "Light", "Dark", "System"
        };

        public ObservableCollection<string> DateFormats { get; } = new()
        {
           "MM/dd/yyyy", "dd/MM/yyyy", "yyyy-MM-dd"
        };

        public ObservableCollection<string> TimeFormats { get; } = new()
        {
           "HH:mm:ss", "hh:mm:ss tt"
        };

        public ObservableCollection<string> Groups { get; } = new()
        {
           "All", "General", "Financial", "Inventory", "Sales"
        };

        public ICommand LoadCommand { get; private set; }
        public ICommand SaveBusinessSettingCommand { get; private set; }
        public ICommand InitializeBusinessSettingsCommand { get; private set; }
        public ICommand ResetPreferencesCommand { get; private set; }
        public ICommand BackupNowCommand { get; private set; }
        public ICommand BrowseCommand { get; private set; }
        public ICommand RestoreCommand { get; private set; }
        public ICommand SaveExchangeRateCommand { get; private set; }
        public ICommand SaveScheduleCommand { get; private set; }
        public ICommand UploadLogoCommand { get; private set; }

        private void InitializeCommands()
        {
            LoadCommand = new AsyncRelayCommand(async _ => await LoadDataAsync());
            SaveBusinessSettingCommand = new AsyncRelayCommand(async _ => await SaveBusinessSettingAsync());
            InitializeBusinessSettingsCommand = new AsyncRelayCommand(async _ => await InitializeBusinessSettingsAsync());
            ResetPreferencesCommand = new AsyncRelayCommand(async _ => await ResetPreferencesAsync());
            BackupNowCommand = new AsyncRelayCommand(async _ => await BackupNowAsync());
            BrowseCommand = new RelayCommand(_ => BrowseForBackupPath());
            RestoreCommand = new AsyncRelayCommand(async _ => await RestoreBackupAsync());
            SaveExchangeRateCommand = new AsyncRelayCommand(async _ => await SaveExchangeRate());
            UploadLogoCommand = new AsyncRelayCommand(async _ => await UploadLogoAsync());
            SaveScheduleCommand = new AsyncRelayCommand(_ => Task.CompletedTask);
        }

        protected override async Task LoadDataAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                Debug.WriteLine("LoadDataAsync skipped - operation in progress");
                return;
            }

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                try
                {
                    await LoadBusinessSettingsAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading business settings: {ex.Message}");
                }

                try
                {
                    await LoadSystemPreferencesAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading system preferences: {ex.Message}");
                }

                try
                {
                    await LoadBackupSettingsAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading backup settings: {ex.Message}");
                }

                try
                {
                    await LoadLogoAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading logo: {ex.Message}");
                }

                try
                {
                    await LoadExchangeRate();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading exchange rate: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error loading settings: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }


        private async Task LoadExchangeRate()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                Debug.WriteLine("LoadExchangeRate skipped - operation in progress");
                return;
            }

            try
            {
                var rateSetting = await _businessSettingsService.GetByKeyAsync("ExchangeRate");
                if (rateSetting != null && decimal.TryParse(rateSetting.Value, out decimal rate))
                {
                    ExchangeRate = rate;
                    CurrencyHelper.UpdateExchangeRate(rate);
                }
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error loading exchange rate: {ex.Message}");
            }
            finally
            {
                _operationLock.Release();
            }
        }
        private async Task LoadLogoAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                Debug.WriteLine("LoadLogoAsync skipped - operation in progress");
                return;
            }

            try
            {
                var logoSetting = await _businessSettingsService.GetByKeyAsync("CompanyLogo");
                if (logoSetting != null && !string.IsNullOrEmpty(logoSetting.Value))
                {
                    try
                    {
                        // Check if the value is a file path
                        if (File.Exists(logoSetting.Value))
                        {
                            // Load the image from file
                            LogoImage = File.ReadAllBytes(logoSetting.Value);
                        }
                        else
                        {
                            // Try to decode as Base64 for backward compatibility
                            try
                            {
                                byte[] imageData = Convert.FromBase64String(logoSetting.Value);
                                LogoImage = imageData;
                            }
                            catch
                            {
                                LogoImage = null;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error loading logo image: {ex.Message}");
                        LogoImage = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error retrieving logo from settings: {ex.Message}");
            }
            finally
            {
                _operationLock.Release();
            }
        }
        private async Task UploadLogoAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Operation already in progress. Please wait.");
                return;
            }

            try
            {
                IsLoading = true;

                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Image files (*.png;*.jpeg;*.jpg)|*.png;*.jpeg;*.jpg|All files (*.*)|*.*",
                    Title = "Select Company Logo"
                };

                bool? result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    openFileDialog.ShowDialog());

                if (result == true)
                {
                    try
                    {
                        // Create app data directory if it doesn't exist
                        string appDataPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "QuickTechSystems");

                        if (!Directory.Exists(appDataPath))
                            Directory.CreateDirectory(appDataPath);

                        // Define logo path and filename
                        string logoFilename = "company_logo.png";
                        string logoPath = Path.Combine(appDataPath, logoFilename);

                        // Resize and save image
                        byte[] imageData = File.ReadAllBytes(openFileDialog.FileName);
                        if (imageData.Length > 1024 * 1024) // If larger than 1MB
                        {
                            imageData = ResizeImage(imageData);
                        }

                        // Save to disk
                        File.WriteAllBytes(logoPath, imageData);

                        // Update local image display
                        LogoImage = imageData;

                        // Save file path to business settings instead of Base64 string
                        await _businessSettingsService.UpdateSettingAsync(
                            "CompanyLogo",
                            logoPath,
                            "CurrentUser"
                        );

                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            MessageBox.Show("Company logo updated successfully.", "Success",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                    }
                    catch (Exception ex)
                    {
                        ShowTemporaryErrorMessage($"Error uploading logo: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error with file dialog: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }
        private byte[] ResizeImage(byte[] imageData)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream(imageData))
                {
                    // Load image
                    BitmapImage originalImage = new BitmapImage();
                    originalImage.BeginInit();
                    originalImage.CacheOption = BitmapCacheOption.OnLoad;
                    originalImage.StreamSource = ms;
                    originalImage.EndInit();

                    // Calculate new dimensions (max 300x150)
                    double scale = Math.Min(300.0 / originalImage.PixelWidth, 150.0 / originalImage.PixelHeight);

                    // Create resized image
                    TransformedBitmap resizedImage = new TransformedBitmap(
                        originalImage,
                        new ScaleTransform(scale, scale)
                    );

                    // Convert back to byte array
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(resizedImage));

                    using (MemoryStream outStream = new MemoryStream())
                    {
                        encoder.Save(outStream);
                        return outStream.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resizing image: {ex.Message}");
                return imageData; // Return original if resize fails
            }
        }

        private async Task LoadBusinessSettingsAsync()
        {
            try
            {
                var settings = await _businessSettingsService.GetAllAsync();
                BusinessSettings = new ObservableCollection<BusinessSettingDTO>(
                    settings.Select(s => s.Clone()).ToList());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading business settings: {ex.Message}");
                throw;
            }
        }

        private async Task LoadSettingsByGroupAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                Debug.WriteLine("LoadSettingsByGroupAsync skipped - operation in progress");
                return;
            }

            try
            {
                IsLoading = true;

                if (SelectedGroup == "All")
                {
                    var settings = await _businessSettingsService.GetAllAsync();
                    BusinessSettings = new ObservableCollection<BusinessSettingDTO>(
                        settings.Select(s => s.Clone()).ToList());
                    return;
                }

                var groupSettings = await _businessSettingsService.GetByGroupAsync(SelectedGroup);
                BusinessSettings = new ObservableCollection<BusinessSettingDTO>(
                    groupSettings.Select(s => s.Clone()).ToList());
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error loading settings: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        private async Task LoadBackupSettingsAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                Debug.WriteLine("LoadBackupSettingsAsync skipped - operation in progress");
                return;
            }

            try
            {
                IsLoading = true;

                var lastBackup = await _backupService.GetLastBackupTimeAsync();
                LastBackupStatus = lastBackup.HasValue
                    ? $"Last backup: {lastBackup.Value:g}"
                    : "No backup has been performed yet";
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error loading backup settings: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        private async Task LoadSystemPreferencesAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                Debug.WriteLine("LoadSystemPreferencesAsync skipped - operation in progress");
                return;
            }

            try
            {
                const string userId = "default";

                CurrentTheme = await _systemPreferencesService.GetPreferenceValueAsync(userId, "Theme", "Light");

                var savedLanguage = await _systemPreferencesService.GetPreferenceValueAsync(userId, "Language", "en-US");
                SelectedLanguage = savedLanguage;

                var soundEffectsStr = await _systemPreferencesService.GetPreferenceValueAsync(userId, "SoundEffects", "true");
                SoundEffectsEnabled = bool.Parse(soundEffectsStr);

                var notificationsStr = await _systemPreferencesService.GetPreferenceValueAsync(userId, "EnableNotifications", "true");
                NotificationsEnabled = bool.Parse(notificationsStr);

                DateFormat = await _systemPreferencesService.GetPreferenceValueAsync(userId, "DateFormat", "MM/dd/yyyy");
                TimeFormat = await _systemPreferencesService.GetPreferenceValueAsync(userId, "TimeFormat", "HH:mm:ss");

                var restaurantModeStr = await _systemPreferencesService.GetPreferenceValueAsync(userId, "RestaurantMode", "false");
                IsRestaurantMode = bool.Parse(restaurantModeStr);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading preferences: {ex.Message}");
                throw;
            }
            finally
            {
                _operationLock.Release();
            }
        }

        private async Task SaveBusinessSettingAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Save operation already in progress. Please wait.");
                return;
            }

            try
            {
                IsLoading = true;

                if (SelectedBusinessSetting == null)
                {
                    ShowTemporaryErrorMessage("No setting selected.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(SelectedBusinessSetting.Value))
                {
                    ShowTemporaryErrorMessage("Setting value cannot be empty.");
                    return;
                }

                await _businessSettingsService.UpdateSettingAsync(
                    SelectedBusinessSetting.Key,
                    SelectedBusinessSetting.Value,
                    "CurrentUser"
                );

                await LoadSettingsByGroupAsync();

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show("Setting updated successfully.", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error saving setting: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        private async Task SaveExchangeRate()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Save operation already in progress. Please wait.");
                return;
            }

            try
            {
                IsLoading = true;

                await _businessSettingsService.UpdateSettingAsync(
                    "ExchangeRate",
                    ExchangeRate.ToString(),
                    "System");

                CurrencyHelper.UpdateExchangeRate(ExchangeRate);

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show("Exchange rate updated successfully", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error saving exchange rate: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        private async Task SavePreferenceAsync(string key, string value)
        {
            if (!await _operationLock.WaitAsync(0))
            {
                Debug.WriteLine("SavePreferenceAsync skipped - operation in progress");
                return;
            }

            try
            {
                IsLoading = true;

                const string userId = "default";
                await _systemPreferencesService.SavePreferenceAsync(userId, key, value);

                Debug.WriteLine($"Preference saved: {key}={value}");
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error saving preference: {ex.Message}");
                Debug.WriteLine($"Error saving preference {key}: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        private async Task InitializeBusinessSettingsAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Operation already in progress. Please wait.");
                return;
            }

            try
            {
                IsLoading = true;

                var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    return MessageBox.Show("This will initialize default business settings. Continue?",
                        "Confirm Initialize", MessageBoxButton.YesNo, MessageBoxImage.Question);
                });

                if (result == MessageBoxResult.Yes)
                {
                    await _businessSettingsService.InitializeDefaultSettingsAsync();
                    await LoadBusinessSettingsAsync();

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("Default settings initialized successfully.", "Success",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error initializing settings: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        private async Task ResetPreferencesAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Reset operation already in progress. Please wait.");
                return;
            }

            try
            {
                IsLoading = true;

                var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    return MessageBox.Show("Are you sure you want to reset all preferences to default?",
                        "Confirm Reset", MessageBoxButton.YesNo, MessageBoxImage.Question);
                });

                if (result == MessageBoxResult.Yes)
                {
                    const string userId = "default";
                    await _systemPreferencesService.InitializeUserPreferencesAsync(userId);
                    await LoadSystemPreferencesAsync();

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("Preferences have been reset to default values.", "Success",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error resetting preferences: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        private async Task BackupNowAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Backup operation already in progress. Please wait.");
                return;
            }

            try
            {
                IsLoading = true;

                if (string.IsNullOrEmpty(BackupPath))
                {
                    BackupPath = "C:\\QuickTechBackup";
                }

                Directory.CreateDirectory(BackupPath);

                await _backupService.CreateBackupAsync(BackupPath);
                await LoadBackupSettingsAsync();

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show("Backup completed successfully.", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error performing backup: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        private void BrowseForBackupPath()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".bak",
                Filter = "Backup files (*.bak)|*.bak|All files (*.*)|*.*",
                Title = "Select Backup Location"
            };

            if (dialog.ShowDialog() == true)
            {
                BackupPath = dialog.FileName;
            }
        }

        private async Task RestoreBackupAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Restore operation already in progress. Please wait.");
                return;
            }

            try
            {
                IsLoading = true;

                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    DefaultExt = ".bak",
                    Filter = "Backup files (*.bak)|*.bak|All files (*.*)|*.*",
                    Title = "Select Backup File to Restore"
                };

                bool? dialogResult = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    dialog.ShowDialog());

                if (dialogResult == true)
                {
                    MessageBoxResult confirmResult = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        MessageBox.Show(
                            "Restoring a backup will replace all current data. Are you sure you want to continue?",
                            "Confirm Restore",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning));

                    if (confirmResult == MessageBoxResult.Yes)
                    {
                        string fileName = dialog.FileName;
                        await _backupService.RestoreBackupAsync(fileName);

                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            MessageBox.Show("Database restored successfully. The application will now restart.",
                                "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                            System.Windows.Application.Current.Shutdown();
                            System.Diagnostics.Process.Start(System.Windows.Application.ResourceAssembly.Location);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error restoring backup: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        protected override void SubscribeToEvents()
        {
            _eventAggregator.Subscribe<EntityChangedEvent<BusinessSettingDTO>>(_settingChangedHandler);
        }

        protected override void UnsubscribeFromEvents()
        {
            _eventAggregator.Unsubscribe<EntityChangedEvent<BusinessSettingDTO>>(_settingChangedHandler);
        }

        private async void HandleSettingChanged(EntityChangedEvent<BusinessSettingDTO> evt)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                switch (evt.Action)
                {
                    case "Create":
                        BusinessSettings.Add(evt.Entity);
                        break;
                    case "Update":
                        var existingSetting = BusinessSettings.FirstOrDefault(s => s.Id == evt.Entity.Id);
                        if (existingSetting != null)
                        {
                            var index = BusinessSettings.IndexOf(existingSetting);
                            BusinessSettings[index] = evt.Entity;
                        }
                        break;
                    case "Delete":
                        var settingToRemove = BusinessSettings.FirstOrDefault(s => s.Id == evt.Entity.Id);
                        if (settingToRemove != null)
                        {
                            BusinessSettings.Remove(settingToRemove);
                        }
                        break;
                }
            });
        }

        private void ShowTemporaryErrorMessage(string message)
        {
            ErrorMessage = message;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            });

            Task.Run(async () =>
            {
                await Task.Delay(5000);
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (ErrorMessage == message)
                    {
                        ErrorMessage = string.Empty;
                    }
                });
            });
        }

        public override void Dispose()
        {
            if (!_isDisposed)
            {
                _operationLock?.Dispose();
                UnsubscribeFromEvents();

                _isDisposed = true;
            }

            base.Dispose();
        }
    }

    public static class BusinessSettingDTOExtensions
    {
        public static BusinessSettingDTO Clone(this BusinessSettingDTO source)
        {
            return new BusinessSettingDTO
            {
                Id = source.Id,
                Key = source.Key,
                Value = source.Value,
                Group = source.Group,
                Description = source.Description,
                DataType = source.DataType,
                IsSystem = source.IsSystem,
                LastModified = source.LastModified,
                ModifiedBy = source.ModifiedBy
            };
        }
    }
}
