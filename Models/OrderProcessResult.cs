namespace WinUIOrderApp.Models
{
    public class OrderProcessResult
    {
        public bool Success
        {
            get; set;
        }
        public int AssembledCodesCount
        {
            get; set;
        }
        public string OutputFilePath { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }
}