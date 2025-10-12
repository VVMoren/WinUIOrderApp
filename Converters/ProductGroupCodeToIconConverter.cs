using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace WinUIOrderApp.Converters
{
    public class ProductGroupCodeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string code || string.IsNullOrWhiteSpace(code))
                return null;
            string iconName = $"{code.ToLower()}.png";
            try
            {
                var resourceUri = new Uri($"/Resources/icon/product_groups/{iconName}", UriKind.Relative);
                var bitmap = new BitmapImage(resourceUri);
                if (bitmap.PixelWidth > 0)
                    return bitmap;
            }
            catch { }
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string filePath = Path.Combine(baseDir, "Resources", "icon", "product_groups", iconName);

                if (File.Exists(filePath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                    bitmap.EndInit();
                    return bitmap;
                }
            }
            catch { }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
  

        private BitmapImage GetDefaultIcon(string baseDir = null)
        {
            baseDir ??= AppDomain.CurrentDomain.BaseDirectory;
            string defaultPath = Path.Combine(baseDir, "Resources", "icon", "product_groups", "default.png");

            if (File.Exists(defaultPath))
            {
                System.Diagnostics.Debug.WriteLine("ProductGroupCodeToIconConverter: using default icon");
                return new BitmapImage(new Uri(defaultPath, UriKind.Absolute));
            }

            System.Diagnostics.Debug.WriteLine("ProductGroupCodeToIconConverter: default icon not found, returning null");
            return null;
        }
    }
}