using System;
using System.Runtime.InteropServices;

namespace WinUIOrderApp.Helpers
{
    public static class FolderBrowserHelper
    {
        public static string BrowseForFolder(string description)
        {
            // Простая реализация с использованием WinForms, но динамически загружаем сборку
            try
            {
                var assembly = System.Reflection.Assembly.Load("System.Windows.Forms");
                if (assembly == null) return string.Empty;
                var fbdType = assembly.GetType("System.Windows.Forms.FolderBrowserDialog");
                if (fbdType == null) return string.Empty;
                using var dlg = Activator.CreateInstance(fbdType) as IDisposable;
                var propDesc = fbdType.GetProperty("Description");
                var propSelected = fbdType.GetProperty("SelectedPath");
                var methodShow = fbdType.GetMethod("ShowDialog", new Type[0]);
                if (propDesc != null) propDesc.SetValue(dlg, description);
                var res = methodShow.Invoke(dlg, null);
                // DialogResult.OK == 1
                if ((int)res == 1 && propSelected != null)
                {
                    return propSelected.GetValue(dlg)?.ToString() ?? string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
            return string.Empty;
        }
    }
}
