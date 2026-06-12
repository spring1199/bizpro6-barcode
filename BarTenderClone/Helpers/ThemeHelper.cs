using System;
using System.Windows;
using ModernWpf;

namespace BarTenderClone.Helpers
{
    public static class ThemeHelper
    {
        public static void ApplyTheme(bool isDark)
        {
            // Set ModernWpf Theme
            ThemeManager.Current.ApplicationTheme = isDark ? ApplicationTheme.Dark : ApplicationTheme.Light;

            // Load Custom Colors Dictionary
            var themeDict = new ResourceDictionary();
            if (isDark)
            {
                themeDict.Source = new Uri("pack://application:,,,/BizPro6Barcode;component/Styles/DarkColors.xaml", UriKind.Absolute);
            }
            else
            {
                themeDict.Source = new Uri("pack://application:,,,/BizPro6Barcode;component/Styles/LightColors.xaml", UriKind.Absolute);
            }

            var appDictionaries = Application.Current.Resources.MergedDictionaries;
            
            // Remove previous theme dictionary
            for (int i = appDictionaries.Count - 1; i >= 0; i--)
            {
                var dict = appDictionaries[i];
                if (dict.Source != null && 
                   (dict.Source.ToString().Contains("DarkColors.xaml") || 
                    dict.Source.ToString().Contains("LightColors.xaml")))
                {
                    appDictionaries.RemoveAt(i);
                }
            }

            // Add the new one
            appDictionaries.Add(themeDict);
        }
    }
}
