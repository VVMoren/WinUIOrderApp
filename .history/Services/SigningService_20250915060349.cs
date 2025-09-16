using System;
using System.IO;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using GostCryptography.Config;
using GostCryptography.Pkcs;

namespace WinUIOrderApp.Services
{
    public static class SigningService
    {
        static SigningService()
        {
            GostCryptoConfig.Initialize();
        }

        // Подписывает строку и возвращает Base64‑строку CMS подписи
        public static Task<string> SignStringAsync(string content, X509Certificate2 cert)
        {
            return Task.Run(() =>
            {
                var contentInfo = new ContentInfo(Encoding.UTF8.GetBytes(content));
                // detached=true - отсоединенная подпись
                var signedCms = new GostSignedCms(contentInfo, true);
                var signer = new CmsSigner(cert);
                signedCms.ComputeSignature(signer);
                var signature = signedCms.Encode();
                return Convert.ToBase64String(signature);
            });
        }

        // Подписывает файл и сохраняет подпись в *.p7s (детачед)
        public static Task<string> SignFileAsync(string filePath, X509Certificate2 cert)
        {
            return Task.Run(() =>
            {
                var data = File.ReadAllBytes(filePath);
                var contentInfo = new ContentInfo(data);
                var signedCms = new GostSignedCms(contentInfo, true);
                var signer = new CmsSigner(cert);
                signedCms.ComputeSignature(signer);
                var signature = signedCms.Encode();
                var signaturePath = Path.ChangeExtension(filePath, ".p7s");
                File.WriteAllBytes(signaturePath, signature);
                return signaturePath;
            });
        }
    }
}
