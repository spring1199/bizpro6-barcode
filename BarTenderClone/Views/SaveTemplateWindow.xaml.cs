using System.Windows;

namespace BarTenderClone.Views
{
    public partial class SaveTemplateWindow : Window
    {
        public string TemplateName { get; private set; } = string.Empty;

        public SaveTemplateWindow(string initialName = "")
        {
            InitializeComponent();
            NameTextBox.Text = initialName;
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show(
                    GetResourceString("MsgEnterTemplateName", "Please enter a template name."), 
                    GetResourceString("MsgConfirmTitle", "Validation Error"), 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Warning);
                return;
            }

            TemplateName = NameTextBox.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private string GetResourceString(string key, string fallback)
        {
            if (Application.Current?.Resources == null) return fallback;
            var resource = Application.Current.TryFindResource(key);
            return resource is string str ? str : fallback;
        }
    }
}
