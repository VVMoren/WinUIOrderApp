using System;
using System.IO;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace WinUIOrderApp.Services
{
    public static class SigningService
    {
        /// <summary>
        /// Подписывает файл детачед (создаёт file.p7s рядом с исходным файлом).
        /// Возвращает путь к созданному .p7s файлу.
        /// </summary>
        public static Task<string> SignFileAsync(string filePath, X509Certificate2 cert)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath is empty", nameof(filePath));
            if (cert == null) throw new ArgumentNullException(nameof(cert));

            return Task.Run(() =>
            {
                // Читаем данные
                var data = File.ReadAllBytes(filePath);

                // Создаём контент и CMS объект (detached = true)
                var contentInfo = new ContentInfo(data);
                var signedCms = new SignedCms(contentInfo, detached: true);

                // Создаём подписанта
                var signer = new CmsSigner(cert)
                {
                    // Включаем сертификат подписанта в подпись (можно настроить)
                    IncludeOption = X509IncludeOption.ExcludeRoot
                };

                // Выполняем подпись
                signedCms.ComputeSignature(signer);

                // Получаем байты подписи (PKCS#7 / CMS)
                var signature = signedCms.Encode();

                // Сохраняем рядом с файлом: sameName.p7s
                var signaturePath = Path.ChangeExtension(filePath, ".p7s");
                File.WriteAllBytes(signaturePath, signature);

                return signaturePath;
            });
        }
    }
}
