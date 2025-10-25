using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WinUIOrderApp.Views.Windows;

namespace WinUIOrderApp.Helpers
{
    public static class NavigationHelper
    {
        public static void NavigateTo(string tag)
        {
            if (Application.Current?.MainWindow is not MainWindow mainWindow)
                return;

            mainWindow.NavigateTo(tag);
        }
    }
}
