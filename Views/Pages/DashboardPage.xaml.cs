using System.Windows.Controls;
using WinUIOrderApp.ViewModels.Pages;

namespace WinUIOrderApp.Views.Pages
{
    public partial class DashboardPage : Page
    {
        public DashboardViewModel ViewModel
        {
            get;
        }

        public DashboardPage()
        {
            InitializeComponent();
            ViewModel = new DashboardViewModel();
            DataContext = ViewModel;
        }
    }
}
