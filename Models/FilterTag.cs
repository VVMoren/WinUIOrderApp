// WinUIOrderApp/Models/FilterTag.cs
namespace WinUIOrderApp.Models
{
    public class FilterTag
    {
        public string Field { get; set; } = "";
        public string Value { get; set; } = "";
        public bool IsInclude { get; set; } = true;

        public string Display => IsInclude ? $"+ {Field} = {Value}" : $"- {Field} != {Value}";
        public string Key => $"{(IsInclude ? "I" : "E")}|{Field}|{Value}";
    }
}
