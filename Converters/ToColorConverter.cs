using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WinUIOrderApp.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? Brushes.SeaGreen : Brushes.IndianRed; // true -> зелёный, false -> красный

            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
