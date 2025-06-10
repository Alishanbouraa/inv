// SystemPreferencesViewModel.cs
using System.Diagnostics;
using QuickTechSystems.Application.Events;

namespace QuickTechSystems.WPF.ViewModels
{
    public class SystemPreferencesViewModel : ViewModelBase
    {
        private readonly ISystemPreferencesService _preferencesService;
        private ObservableCollection<SystemPreferenceDTO> _preferences;
        private string _currentTheme;
        private string _currentLanguage;
        private bool _soundEffectsEnabled;
        private bool _notificationsEnabled;
        private string _dateFormat;
        private string _timeFormat;
        private bool _isRestaurantMode; // Added missing field

        public SystemPreferencesViewModel(
            ISystemPreferencesService preferencesService,
            IEventAggregator eventAggregator) : base(eventAggregator)
        {
            _preferencesService = preferencesService;
            _preferences = new ObservableCollection<SystemPreferenceDTO>();
            _currentTheme = "Light";
            _currentLanguage = "en-US";
            _dateFormat = "MM/dd/yyyy";
            _timeFormat = "HH:mm:ss";
            _isRestaurantMode = false; // Initialize with default value

            LoadCommand = new AsyncRelayCommand(async _ => await LoadPreferencesAsync());
            ResetCommand = new AsyncRelayCommand(async _ => await ResetPreferencesAsync());

            _ = LoadPreferencesAsync();
        }

        public ObservableCollection<SystemPreferenceDTO> Preferences
        {
            get => _preferences;
            set => SetProperty(ref _preferences, value);
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

        // Added missing property
        public bool IsRestaurantMode
        {
            get => _isRestaurantMode;
            set
            {
                if (SetProperty(ref _isRestaurantMode, value))
                {
                    _ = SavePreferenceAsync("RestaurantMode", value.ToString());

                    // Publish event to update the application immediately
                    _eventAggregator.Publish(new ApplicationModeChangedEvent(value));
                    Debug.WriteLine($"Restaurant mode changed to: {value}");
                }
            }
        }

        public ObservableCollection<string> AvailableThemes { get; } = new()
       {
           "Light", "Dark", "System"
       };

        public ObservableCollection<string> AvailableLanguages { get; } = new()
       {
           "en-US", "es-ES", "fr-FR", "de-DE"
       };

        public ObservableCollection<string> DateFormats { get; } = new()
       {
           "MM/dd/yyyy", "dd/MM/yyyy", "yyyy-MM-dd"
       };

        public ObservableCollection<string> TimeFormats { get; } = new()
       {
           "HH:mm:ss", "hh:mm:ss tt"
       };

        public ICommand LoadCommand { get; }
        public ICommand ResetCommand { get; }

        protected override void SubscribeToEvents()
        {
            // Add any event subscriptions if needed
        }

        protected override void UnsubscribeFromEvents()
        {
            // Remove any event subscriptions if needed
        }

        private async Task LoadPreferencesAsync()
        {
            try
            {
                const string userId = "default";
                var preferences = await _preferencesService.GetUserPreferencesAsync(userId);
                Preferences = new ObservableCollection<SystemPreferenceDTO>(preferences);

                // Get current values and log them
                CurrentTheme = await _preferencesService.GetPreferenceValueAsync(userId, "Theme", "Light");
                CurrentLanguage = await _preferencesService.GetPreferenceValueAsync(userId, "Language", "en-US");
                SoundEffectsEnabled = bool.Parse(await _preferencesService.GetPreferenceValueAsync(userId, "SoundEffects", "true"));
                NotificationsEnabled = bool.Parse(await _preferencesService.GetPreferenceValueAsync(userId, "EnableNotifications", "true"));
                DateFormat = await _preferencesService.GetPreferenceValueAsync(userId, "DateFormat", "MM/dd/yyyy");
                TimeFormat = await _preferencesService.GetPreferenceValueAsync(userId, "TimeFormat", "HH:mm:ss");

                // Add logging for restaurant mode
                var restaurantModeStr = await _preferencesService.GetPreferenceValueAsync(userId, "RestaurantMode", "false");
                bool restaurantMode = bool.Parse(restaurantModeStr);
                Debug.WriteLine($"Loaded RestaurantMode preference: {restaurantMode}");

                // Set the property using the field to avoid triggering save
                _isRestaurantMode = restaurantMode;
                OnPropertyChanged(nameof(IsRestaurantMode));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading preferences: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SavePreferenceAsync(string key, string value)
        {
            try
            {
                const string userId = "default";
                await _preferencesService.SavePreferenceAsync(userId, key, value);
                Debug.WriteLine($"Preference saved: {key}={value}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving preference: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ResetPreferencesAsync()
        {
            try
            {
                if (MessageBox.Show("Are you sure you want to reset all preferences to default?",
                    "Confirm Reset", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    const string userId = "default";
                    await _preferencesService.InitializeUserPreferencesAsync(userId);
                    await LoadPreferencesAsync();
                    MessageBox.Show("Preferences have been reset to default values.", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error resetting preferences: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}