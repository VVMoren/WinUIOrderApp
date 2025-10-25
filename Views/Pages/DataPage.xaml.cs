using System.Windows.Controls;
using WinUIOrderApp.ViewModels.Pages;

namespace WinUIOrderApp.Views.Pages
{
    public partial class DataPage : Page
    {
        public DataPageViewModel ViewModel
        {
            get;
        }

        public DataPage(DataPageViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;

            InitializeComponent();

            Loaded += async (_, __) => await ViewModel.LoadLatestDataAsync();
        }
    }
}
