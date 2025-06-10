// QuickTechSystems/Helpers/LanguageManager.cs
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using QuickTechSystems.Application.Services.Interfaces;

namespace QuickTechSystems.Helpers
{
    public class LanguageManager
    {
        private readonly ISystemPreferencesService _preferencesService;
        private readonly Dispatcher _uiDispatcher;
        private const string DefaultLanguage = "en-US";
        public event EventHandler<string>? LanguageChanged;

        public LanguageManager(ISystemPreferencesService preferencesService)
        {
            _preferencesService = preferencesService;
            _uiDispatcher = System.Windows.Application.Current.Dispatcher;

            // Don't call InitializeDefaultLanguageAsync here - it will be called explicitly from App.xaml.cs
        }

        public static ObservableCollection<LanguageInfo> AvailableLanguages { get; } = new()
        {
            new LanguageInfo { Code = "en-US", Name = "English", NativeName = "English" },
            new LanguageInfo { Code = "fr-FR", Name = "French", NativeName = "Français" },
            new LanguageInfo { Code = "ar-SA", Name = "Arabic", NativeName = "العربية", FlowDirection = FlowDirection.RightToLeft }
        };

        public async Task SetLanguage(string languageCode)
        {
            try
            {
                // Ensure we're on the UI thread
                if (_uiDispatcher.CheckAccess())
                {
                    await ApplyLanguageChangeAsync(languageCode);
                }
                else
                {
                    await _uiDispatcher.InvokeAsync(async () =>
                    {
                        await ApplyLanguageChangeAsync(languageCode);
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting language: {ex.Message}");

                // Fallback to default language if there's an error
                if (languageCode != DefaultLanguage)
                {
                    Debug.WriteLine("Falling back to default language");
                    await SetLanguage(DefaultLanguage);
                }
            }
        }

        private async Task ApplyLanguageChangeAsync(string languageCode)
        {
            try
            {
                var resourceDictionary = new ResourceDictionary
                {
                    Source = new Uri($"/Resources/Dictionaries/Languages/{languageCode}.xaml", UriKind.Relative)
                };

                // Remove existing language dictionary
                var existingDict = System.Windows.Application.Current.Resources.MergedDictionaries
                    .FirstOrDefault(d => d.Source?.OriginalString.Contains("/Languages/") ?? false);

                if (existingDict != null)
                    System.Windows.Application.Current.Resources.MergedDictionaries.Remove(existingDict);

                System.Windows.Application.Current.Resources.MergedDictionaries.Add(resourceDictionary);

                // Save preference in a separate task to avoid transaction conflicts
                await Task.Run(async () =>
                {
                    try
                    {
                        await _preferencesService.SavePreferenceAsync("default", "Language", languageCode);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error saving language preference: {ex.Message}");
                    }
                });

                // Update FlowDirection for the entire application
                var languageInfo = AvailableLanguages.FirstOrDefault(l => l.Code == languageCode);
                if (languageInfo != null && System.Windows.Application.Current.MainWindow != null)
                {
                    System.Windows.Application.Current.MainWindow.FlowDirection = languageInfo.FlowDirection;
                }

                // Notify subscribers about the language change
                LanguageChanged?.Invoke(this, languageCode);

                Debug.WriteLine($"Language changed to: {languageCode}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in ApplyLanguageChange: {ex.Message}");
                throw;
            }
        }

        public FlowDirection GetFlowDirection(string languageCode)
        {
            var languageInfo = AvailableLanguages.FirstOrDefault(l => l.Code == languageCode);
            return languageInfo?.FlowDirection ?? FlowDirection.LeftToRight;
        }
    }

    public class LanguageInfo
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string NativeName { get; set; } = string.Empty;
        public FlowDirection FlowDirection { get; set; } = FlowDirection.LeftToRight;
    }
}