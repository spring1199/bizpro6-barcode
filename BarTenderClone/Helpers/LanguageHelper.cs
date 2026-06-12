using Newtonsoft.Json;
using System;
using System.IO;
using System.Windows;

namespace BarTenderClone.Helpers
{
    public class LanguageHelper
    {
        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BarTenderClone");

        private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

        private static string _currentLanguage = "mn";

        public static string CurrentLanguage => _currentLanguage;

        public static event EventHandler? LanguageChanged;

        public static void ApplyLanguage(string langCode)
        {
            if (string.IsNullOrWhiteSpace(langCode))
                langCode = "mn";

            langCode = langCode.ToLowerInvariant();
            if (langCode != "mn" && langCode != "en")
                langCode = "mn";

            _currentLanguage = langCode;

            // Load Custom Language Dictionary
            var langDict = new ResourceDictionary();
            if (langCode == "en")
            {
                langDict.Source = new Uri("pack://application:,,,/BizPro6Barcode;component/Styles/Strings.en.xaml", UriKind.Absolute);
            }
            else
            {
                langDict.Source = new Uri("pack://application:,,,/BizPro6Barcode;component/Styles/Strings.mn.xaml", UriKind.Absolute);
            }

            var appDictionaries = Application.Current.Resources.MergedDictionaries;

            // Remove previous strings dictionary
            for (int i = appDictionaries.Count - 1; i >= 0; i--)
            {
                var dict = appDictionaries[i];
                if (dict.Source != null &&
                   (dict.Source.ToString().Contains("Strings.en.xaml") ||
                    dict.Source.ToString().Contains("Strings.mn.xaml")))
                {
                    appDictionaries.RemoveAt(i);
                }
            }

            // Add the new one
            appDictionaries.Add(langDict);

            // Save settings to file
            SaveLanguageSetting(langCode);

            // Raise language changed event
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }

        public static string LoadLanguage()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                    if (settings != null && !string.IsNullOrWhiteSpace(settings.Language))
                    {
                        var lang = settings.Language.ToLowerInvariant();
                        if (lang == "en" || lang == "mn")
                        {
                            ApplyLanguage(lang);
                            return lang;
                        }
                    }
                }
            }
            catch
            {
                // Fallback to default
            }

            ApplyLanguage("mn");
            return "mn";
        }

        private static void SaveLanguageSetting(string langCode)
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                var settings = new AppSettings { Language = langCode };
                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Ignore save errors
            }
        }
    }

    public class AppSettings
    {
        public string Language { get; set; } = "mn";
    }
}
