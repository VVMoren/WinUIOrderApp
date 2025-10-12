namespace WinUIOrderApp.Models
{
    public class ExpanderDataItem
    {
        public string Brand { get; set; } = string.Empty;
        public string Gtin { get; set; } = string.Empty;
        public string Series { get; set; } = string.Empty;
        public string Flavor { get; set; } = string.Empty;
        public int TotalCount
        {
            get; set;
        }
        public int AvailableCount
        {
            get; set;
        }
        public int UsedCount
        {
            get; set;
        }
    }
}