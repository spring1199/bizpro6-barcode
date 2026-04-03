    using BarTenderClone.Services;
using BarTenderClone.ViewModels;
using BarTenderClone.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System.Windows;
using System.Net.Http;

namespace BarTenderClone
{
    public partial class App : Application
    {
        public static IHost? AppHost { get; private set; }

        public App()
        {
            AppHost = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.SetBasePath(AppContext.BaseDirectory);
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    var baseUrl = hostContext.Configuration["ApiSettings:BaseUrl"]!;

                    // Add logging service first
                    services.AddSingleton<ILoggingService, FileLoggingService>();

                    services.AddSingleton<ISessionService, SessionService>();
                    services.AddHttpClient<IAuthenticationService, AuthenticationService>(client =>
                        client.BaseAddress = new System.Uri(baseUrl));
                    services.AddHttpClient<IApiService, ApiService>(client =>
                        client.BaseAddress = new System.Uri(baseUrl));
                    services.AddSingleton<IZplGeneratorService, ZplGeneratorService>();

                    // Updated PrintService with logging dependency and API service
                    services.AddSingleton<IPrintService>(sp => new PrintService(
                        sp.GetRequiredService<IZplGeneratorService>(),
                        sp.GetRequiredService<ILoggingService>(),
                        sp.GetRequiredService<IApiService>()
                    ));

                    services.AddSingleton<ITemplateService, TemplateService>();

                    services.AddTransient<LoginViewModel>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<LabelPreviewViewModel>(provider =>
                new LabelPreviewViewModel(
                    provider.GetRequiredService<IApiService>(),
                    provider.GetRequiredService<IPrintService>(),
                    provider.GetRequiredService<ISessionService>(),
                    provider.GetRequiredService<ITemplateService>()
                ));

            services.AddSingleton<MainWindow>(s => new MainWindow
                    {
                        DataContext = s.GetRequiredService<MainViewModel>()
                    });
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            await AppHost!.StartAsync();

            // Wire up global exception handler to prevent crashes
            this.DispatcherUnhandledException += OnDispatcherUnhandledException;

            var startupForm = AppHost.Services.GetRequiredService<MainWindow>();
            startupForm.Show();

            base.OnStartup(e);
        }


        protected override async void OnExit(ExitEventArgs e)
        {
            await AppHost!.StopAsync();
            base.OnExit(e);
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // Log the exception
            var logger = AppHost?.Services.GetService<ILoggingService>();
            if (logger != null)
            {
                logger.LogError("Unhandled UI Exception", e.Exception);
            }

            // Show error to user
            MessageBox.Show($"An unexpected error occurred: {e.Exception.Message}\n\nSee logs for details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            
            // Prevent default crash behavior
            e.Handled = true;
        }
    }
}
