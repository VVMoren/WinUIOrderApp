using System;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Data;

namespace WinUIOrderApp.Converters
{
    public class CertificateSimpleNameConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not X509Certificate2 certificate)
            {
                return string.Empty;
            }

            var simpleName = certificate.GetNameInfo(X509NameType.SimpleName, false);
            if (string.IsNullOrWhiteSpace(simpleName))
            {
                simpleName = certificate.SubjectName?.Name ?? certificate.Subject;
            }

            return string.IsNullOrWhiteSpace(simpleName) ? "Безымянный сертификат" : simpleName;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
