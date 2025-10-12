using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WinUIOrderApp.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status.ToLower() switch
                {
                    "использовано" => new SolidColorBrush(Colors.Red),
                    "не использовано" => new SolidColorBrush(Colors.Green),
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}