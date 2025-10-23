namespace WinUIOrderApp.Models
{
    public class ProductGroupDto
    {
        public int id
        {
            get; set;
        }
        public string name { get; set; } = "";
        public string code { get; set; } = "";
        public string? url
        {
            get; set;
        }
        public string? startDate
        {
            get; set;
        }
        public string? description
        {
            get; set;
        }
        public string? productGroupStatus
        {
            get; set;
        }
        public object[]? tnvedDtoSet
        {
            get; set;
        }
        public bool farmer
        {
            get; set;
        }

        //public bool IsEnabled { get; set; } = false;

        public override string ToString() => $"{name} ({code})";
    }
}