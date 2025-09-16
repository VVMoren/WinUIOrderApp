namespace markapp.Models
{
    public class ProductGroupDto
    {
        public int id { get; set; }
        public string name { get; set; } = string.Empty;
        public string code { get; set; } = string.Empty;
        public string productGroupStatus { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = false;
    }

}
