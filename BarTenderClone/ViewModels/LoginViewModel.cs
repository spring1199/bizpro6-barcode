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
                    ErrorMessage = "Please fill in all fields.";
                    return;
                }

                bool result = await _authService.LoginAsync(TenancyName, Username, password);

                if (result)
                {
                    LoginSuccess?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    ErrorMessage = "Login failed. Check credentials.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"An error occurred: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
