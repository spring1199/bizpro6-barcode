using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace BarTenderClone.Views
{
    public partial class LoadTemplateWindow : Window
    {
        public string SelectedTemplate { get; private set; } = string.Empty;
        public bool IsDeleteRequested { get; private set; } = false;

        public LoadTemplateWindow(IEnumerable<string> templates)
        {
            InitializeComponent();
            TemplatesListBox.ItemsSource = templates;
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (TemplatesListBox.SelectedItem == null)
            {
                MessageBox.Show(
                    GetResourceString("MsgSelectTemplate", "Please select a template."), 
                    GetResourceString("MsgConfirmTitle", "Validation Error"), 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Warning);
                return;
            }

            SelectedTemplate = TemplatesListBox.SelectedItem.ToString() ?? string.Empty;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
             if (TemplatesListBox.SelectedItem == null)
            {
                MessageBox.Show(
                    GetResourceString("MsgSelectTemplateDelete", "Please select a template to delete."), 
                    GetResourceString("MsgConfirmTitle", "Delete"), 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                string.Format(GetResourceString("MsgDeleteTemplateConfirm", "Are you sure you want to delete '{0}'?"), TemplatesListBox.SelectedItem), 
                GetResourceString("MsgConfirmDeleteTitle", "Confirm Delete"), 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                SelectedTemplate = TemplatesListBox.SelectedItem.ToString() ?? string.Empty;
                IsDeleteRequested = true;
                DialogResult = true; // Return true but flag deletion
                Close();
            }
        }

        private void TemplatesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TemplatesListBox.SelectedItem != null)
            {
                LoadButton_Click(sender, e);
            }
        }

        private string GetResourceString(string key, string fallback)
        {
            if (Application.Current?.Resources == null) return fallback;
            var resource = Application.Current.TryFindResource(key);
            return resource is string str ? str : fallback;
        }
    }
}
