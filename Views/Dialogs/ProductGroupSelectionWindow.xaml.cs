using System.Windows;
using System.Windows.Input;
using WinUIOrderApp.ViewModels.Pages;

namespace WinUIOrderApp.Views.Windows
{
    public partial class ProductGroupSelectionWindow : Window
    {
        public ProductGroupSelectionWindow()
        {
            InitializeComponent();
            DataContext = new SettingsViewModel();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }
    }
}