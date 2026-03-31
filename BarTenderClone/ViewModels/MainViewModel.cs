using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace BarTenderClone.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty]
        private object _currentView;

        public MainViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            
            // Show Login View initially
            ShowLogin();
        }

        private void ShowLogin()
        {
            var loginVm = _serviceProvider.GetRequiredService<LoginViewModel>();
            loginVm.LoginSuccess += LoginVm_LoginSuccess;
            CurrentView = loginVm;
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
                // Navigate to App View (Data Fetching / Label Design)
                // For now, implemented as a placeholder or empty Dashboard
                // We can resolve DashboardViewModel later
                // CurrentView = _serviceProvider.GetRequiredService<DashboardViewModel>();
                
                // Placeholder:
                // CurrentView = new TextBlock { Text = "LoggedIn!" }; 
                // Better: use a proper view model
                CurrentView = _serviceProvider.GetRequiredService<LabelPreviewViewModel>(); // We'll create this next
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to load application view: {ex.Message}", "Navigation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                // Optionally log using a logger if available, or just re-show login?
                // For now, let's keep them on login or just show the error. 
                // Since we unsubscribed, we might need to re-subscribe if we want them to try again, 
                // but usually this is a fatal config error.
            }
        }
    }
}
