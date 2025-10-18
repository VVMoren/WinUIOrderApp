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
            if (value is string code)
            {
                try
                {
                    // Правильный путь к иконкам
                    var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                        "Resources", "icon", "product_groups", $"{code}.png");

                    if (File.Exists(iconPath))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(iconPath, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        return bitmap;
                    }
                    else
                    {
                        // Если иконка не найдена, возвращаем null (будет пустое изображение)
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    // Логируем ошибку, но не падаем
                    System.Diagnostics.Debug.WriteLine($"Ошибка загрузки иконки для {code}: {ex.Message}");
                    return null;
                }
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}