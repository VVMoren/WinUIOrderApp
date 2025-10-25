using System.Windows.Controls;
using WinUIOrderApp.ViewModels.Pages;

namespace WinUIOrderApp.Views.Pages
{
    public partial class DataPage : Page
    {
        private readonly DataPageViewModel _viewModel;

        public DataPage(DataPageViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }
    }
}
