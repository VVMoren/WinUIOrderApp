namespace markapp.Models
{
    public class AppConfig
    {
        public string ConfigurationsFolder { get; set; }

        public string AppPropertiesFileName { get; set; }

        public string CertificateName { get; set; } = "ГОЛУБЕЦ ВЛАДИСЛАВ ВИТАЛЬЕВИЧ";
        public string CryptcpPath { get; set; } = @"H:\Token_4z\cryptcp.win32.exe";
        public string DataFilePath { get; set; } = @"H:\Token_4z\data.txt";
        public string SignedFilePath { get; set; } = @"H:\Token_4z\data_sign.txt";
        public string CertificateFingerprint { get; set; } = "C333E83AE307708B1CD5EB8EB94DB0D37CDF1C51";
        public string INN { get; set; } = "771683739093";



    }
}
