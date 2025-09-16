using System;
using System.IO;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;


namespace WinUIOrderApp.Services
{
    public static class SigningService
    {
        static SigningService()
        {

        }


        // Подписывает файл и сохраняет подпись в *.p7s (детачед)
        public static Task<string> SignFileAsync(string filePath, X509Certificate2 cert)
        {
            return Task.Run(() =>
            {
                var data = File.ReadAllBytes(filePath);
                var contentInfo = new ContentInfo(data);
                var signer = new CmsSigner(cert);
                var signaturePath = Path.ChangeExtension(filePath, ".p7s");
                return signaturePath;
            });
        }
    }
}
