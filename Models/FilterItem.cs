// Models/FilterItem.cs
namespace WinUIOrderApp.Models
{
    public enum FilterField
    {
        name, create, ip, inn
    }
    public enum FilterMode
    {
        Include, Exclude
    }

    public sealed class FilterItem
    {
        public FilterField Field
        {
            get; init;
        }
        public string Value { get; init; } = "";
        public FilterMode Mode
        {
            get; init;
        }
        public override string ToString() =>
            Mode == FilterMode.Include ? $"{Field}={Value}" : $"{Field}!={Value}";
    }
}
