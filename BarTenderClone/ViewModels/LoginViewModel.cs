using BarTenderClone.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace BarTenderClone.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IAuthenticationService _authService;

        [ObservableProperty]
        private string _tenancyName;

        [ObservableProperty]
        private string _username;

        // Note: Password binding in WPF is tricky due to security (PasswordBox). 
        // We will bind it via a behavior or separate property, 
        // or just pass it as a parameter from the View code-behind to the Command.
        // For simplicity in MVVM, we might use a bound string if security isn't top-critical, 
        // but PasswordBox with CommandParameter is better.

        [ObservableProperty]
        private string _errorMessage;

        [ObservableProperty]
        private bool _isBusy;

        public event EventHandler LoginSuccess;

        public LoginViewModel(IAuthenticationService authService)
        {
            _authService = authService;

            // Subscribe to language changes using weak event manager
            System.Windows.WeakEventManager<Helpers.LanguageHelper, System.EventArgs>.AddHandler(
                null, 
                nameof(Helpers.LanguageHelper.LanguageChanged), 
                OnLanguageChanged);
        }

        [RelayCommand]
        private async Task LoginAsync(object passwordBox)
        {
            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;

                string password = (passwordBox as System.Windows.Controls.PasswordBox)?.Password;

                if (string.IsNullOrWhiteSpace(TenancyName) || string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
                {
                    _lastErrorKey = "ErrFillFields";
                    _lastErrorParam = string.Empty;
                    ErrorMessage = GetResourceString(_lastErrorKey, "Please fill in all fields.");
                    return;
                }

                bool result = await _authService.LoginAsync(TenancyName, Username, password);

                if (result)
                {
                    LoginSuccess?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    _lastErrorKey = "ErrLoginFailed";
                    _lastErrorParam = string.Empty;
                    ErrorMessage = GetResourceString(_lastErrorKey, "Login failed. Check credentials.");
                }
            }
            catch (Exception ex)
            {
                _lastErrorKey = "ErrAnErrorOccurred";
                _lastErrorParam = ex.Message;
                ErrorMessage = $"{GetResourceString(_lastErrorKey, "An error occurred: ")}{ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string _lastErrorKey = string.Empty;
        private string _lastErrorParam = string.Empty;

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_lastErrorKey))
            {
                var baseStr = GetResourceString(_lastErrorKey, string.Empty);
                ErrorMessage = baseStr + _lastErrorParam;
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
    }
}
