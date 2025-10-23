using System.Windows.Controls;
using WinUIOrderApp.ViewModels.Pages;

namespace WinUIOrderApp.Views.Pages
{
    public partial class NationalCatalogPage : Page
    {
        public NationalCatalogViewModel ViewModel
        {
            get;
        }

        public NationalCatalogPage()
        {
            InitializeComponent();
            ViewModel = new NationalCatalogViewModel();
            DataContext = ViewModel;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            ViewModel.Activate();
        }

        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            ViewModel.Deactivate();
        }
    }
}
