using BarTenderClone.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;

using System.Collections.ObjectModel;
using System.Linq;

namespace BarTenderClone.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ISessionService _sessionService;
        private readonly ILoggingService _loggingService;
        private LabelPreviewViewModel? _labelPreviewVm;

        public ObservableCollection<LanguageOption> AvailableLanguages { get; } = new()
        {
            new LanguageOption { Code = "mn", DisplayName = "Монгол" },
            new LanguageOption { Code = "en", DisplayName = "English" }
        };

        [ObservableProperty]
        private LanguageOption _selectedLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLoggedIn))]
        private object _currentView = null!;

        [ObservableProperty]
        private bool _isDarkMode;

        public bool IsLoggedIn => CurrentView != null && !(CurrentView is LoginViewModel);

        public MainViewModel(IServiceProvider serviceProvider, ISessionService sessionService, ILoggingService loggingService)
        {
            _serviceProvider = serviceProvider;
            _sessionService = sessionService;
            _loggingService = loggingService;
            
            var currentLang = Helpers.LanguageHelper.CurrentLanguage;
            _selectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == currentLang) 
                               ?? AvailableLanguages[0];
            
            // Show Login View initially
            ShowLogin();

            // Subscribe to language changes using weak event manager
            System.Windows.WeakEventManager<Helpers.LanguageHelper, System.EventArgs>.AddHandler(
                null, 
                nameof(Helpers.LanguageHelper.LanguageChanged), 
                OnLanguageChanged);
        }

        partial void OnSelectedLanguageChanged(LanguageOption value)
        {
            if (value != null && value.Code != Helpers.LanguageHelper.CurrentLanguage)
            {
                Helpers.LanguageHelper.ApplyLanguage(value.Code);
            }
        }

        private void ShowLogin()
        {
            var loginVm = _serviceProvider.GetRequiredService<LoginViewModel>();
            loginVm.LoginSuccess += LoginVm_LoginSuccess;
            CurrentView = loginVm;
        }

        [RelayCommand]
        private void Logout()
        {
            var result = MessageBox.Show(
                GetResourceString("MsgLogoutConfirm", "Are you sure you want to log out?"),
                GetResourceString("MsgLogoutTitle", "Logout"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Unsubscribe from SessionExpired
                if (_labelPreviewVm != null)
                {
                    _labelPreviewVm.SessionExpired -= OnSessionExpired;
                    _labelPreviewVm = null;
                }

                // Clear session data
                _sessionService.AccessToken = null;
                _sessionService.TenantId = null;
                _sessionService.TokenExpiresAt = null;

                _loggingService.LogInfo("User manually logged out.");

                // Show login screen
                ShowLogin();
            }
        }

        [RelayCommand]
        private void SwitchToProductData()
        {
            if (_labelPreviewVm != null)
            {
                _labelPreviewVm.ViewMode = AppViewMode.ProductData;
                CurrentView = _labelPreviewVm;
            }
        }

        [RelayCommand]
        private void SwitchToLabelDesigner()
        {
            if (_labelPreviewVm != null)
            {
                _labelPreviewVm.ViewMode = AppViewMode.Designer;
                CurrentView = _labelPreviewVm;
            }
            else
            {
                CurrentView = new PlaceholderViewModel("Label Designer", "📐");
            }
        }

        [RelayCommand]
        private void SwitchToHistory()
        {
            try
            {
                var historyVm = _serviceProvider.GetRequiredService<HistoryViewModel>();
                CurrentView = historyVm;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to load History view", ex);
                CurrentView = new PlaceholderViewModel("History", "📋");
            }
        }

        [RelayCommand]
        private void SwitchToSettings()
        {
            try
            {
                var settingsVm = _serviceProvider.GetRequiredService<SettingsViewModel>();
                CurrentView = settingsVm;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to load Settings view", ex);
                CurrentView = new PlaceholderViewModel("Settings", "⚙️");
            }
        }

        partial void OnIsDarkModeChanged(bool value)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Helpers.ThemeHelper.ApplyTheme(value);
            });
        }

        private void OnSessionExpired(object? sender, EventArgs e)
        {
            if (_labelPreviewVm != null)
            {
                _labelPreviewVm.SessionExpired -= OnSessionExpired;
                _labelPreviewVm = null;
            }
            // SessionExpired may fire from a background thread (async HTTP callback).
            // Dispatch to UI thread before touching bound properties or showing dialogs.
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                System.Windows.MessageBox.Show(
                    GetResourceString("MsgSessionExpired", "Your session has expired. Please log in again."),
                    GetResourceString("MsgSessionExpiredTitle", "Session Expired"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                ShowLogin();
            });
        }

        private void LoginVm_LoginSuccess(object sender, EventArgs e)
        {
            // Unsubscribe
            if (sender is LoginViewModel loginVm)
            {
                loginVm.LoginSuccess -= LoginVm_LoginSuccess;
            }
            
            try
            {
                // Resolve and store LabelPreviewViewModel
                _labelPreviewVm = _serviceProvider.GetRequiredService<LabelPreviewViewModel>();
                _labelPreviewVm.SessionExpired += OnSessionExpired;
                CurrentView = _labelPreviewVm;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"{GetResourceString("MsgNavErrorPrefix", "Failed to load application view: ")}{ex.Message}", 
                    GetResourceString("MsgNavErrorTitle", "Navigation Error"), 
                    System.Windows.MessageBoxButton.OK, 
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private string GetResourceString(string key, string fallback)
        {
            if (System.Windows.Application.Current == null) return fallback;
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                var val = System.Windows.Application.Current.TryFindResource(key);
                return val as string ?? fallback;
            }
            else
            {
                return System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var val = System.Windows.Application.Current.TryFindResource(key);
                    return val as string ?? fallback;
                });
            }
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            var currentLang = Helpers.LanguageHelper.CurrentLanguage;
            if (SelectedLanguage == null || SelectedLanguage.Code != currentLang)
            {
                SelectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == currentLang);
            }
        }
    }
}
