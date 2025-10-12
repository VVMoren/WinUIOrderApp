using System;

namespace WinUIOrderApp.Models
{
    public class FilterTag
    {
        public string Field { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool IsInclude
        {
            get; set;
        }

        public string Key => $"{Field}_{Value}_{IsInclude}";

        public override bool Equals(object obj)
        {
            return obj is FilterTag tag && Key == tag.Key;
        }

        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }
    }
}