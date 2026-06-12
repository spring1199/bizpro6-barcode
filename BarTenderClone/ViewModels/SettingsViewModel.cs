using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;

namespace BarTenderClone.ViewModels
{
    public class LanguageOption
    {
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public partial class SettingsViewModel : ObservableObject
    {
        public ObservableCollection<LanguageOption> AvailableLanguages { get; } = new()
        {
            new LanguageOption { Code = "mn", DisplayName = "Монгол" },
            new LanguageOption { Code = "en", DisplayName = "English" }
        };

        [ObservableProperty]
        private LanguageOption? _selectedLanguage;

        public SettingsViewModel()
        {
            var currentLang = Helpers.LanguageHelper.CurrentLanguage;
            _selectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == currentLang) 
                               ?? AvailableLanguages.FirstOrDefault();

            // Subscribe to language changes using weak event manager
            System.Windows.WeakEventManager<Helpers.LanguageHelper, System.EventArgs>.AddHandler(
                null, 
                nameof(Helpers.LanguageHelper.LanguageChanged), 
                OnLanguageChanged);
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            var currentLang = Helpers.LanguageHelper.CurrentLanguage;
            if (SelectedLanguage == null || SelectedLanguage.Code != currentLang)
            {
                SelectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == currentLang);
            }
        }

        partial void OnSelectedLanguageChanged(LanguageOption? value)
        {
            if (value != null && value.Code != Helpers.LanguageHelper.CurrentLanguage)
            {
                Helpers.LanguageHelper.ApplyLanguage(value.Code);
            }
        }
    }
}
