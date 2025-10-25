using System;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Data;

namespace WinUIOrderApp.Converters
{
    public class CertificateIssuerConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not X509Certificate2 certificate)
            {
                return string.Empty;
            }

            var issuer = certificate.GetNameInfo(X509NameType.SimpleName, true);
            if (string.IsNullOrWhiteSpace(issuer))
            {
                issuer = certificate.IssuerName?.Name ?? certificate.Issuer;
            }

            return string.IsNullOrWhiteSpace(issuer) ? "Неизвестный издатель" : issuer;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
