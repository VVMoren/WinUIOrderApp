using Microsoft.Win32;

namespace WinUIOrderApp.Helpers
{
    public static class FolderBrowserHelper
    {
        public static string BrowseForFolder(string description)
        {
            // ���������� ��� WinForms: ���������� OpenFolderDialog
            var dialog = new OpenFolderDialog
            {
                Title = description,
                Multiselect = false
            };

            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                return dialog.FolderName;
            }

            return string.Empty;
        }
    }
}
