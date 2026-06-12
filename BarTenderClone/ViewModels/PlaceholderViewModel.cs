using CommunityToolkit.Mvvm.ComponentModel;

namespace BarTenderClone.ViewModels
{
    public partial class PlaceholderViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private string _icon;

        public PlaceholderViewModel(string title, string icon)
        {
            Title = title;
            Icon = icon;
        }
    }
}
