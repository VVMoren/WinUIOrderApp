using System.Windows;

namespace WinUIOrderApp.Views.Windows
{
    public partial class ProductGroupSelectionWindow : Window
    {
        public ProductGroupSelectionWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}