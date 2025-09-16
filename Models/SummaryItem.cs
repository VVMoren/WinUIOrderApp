// WinUIOrderApp/Models/SummaryItem.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinUIOrderApp.Models
{
    // Сделан partial — если где-то есть другая часть, компилятор примет оба определения.
    public partial class SummaryItem : INotifyPropertyChanged
    {
        public string Name { get; set; } = "";
        public string Ip { get; set; } = "";
        public string Creator { get; set; } = ""; // Created / продавец
        public int TotalByName { get; set; } = 0;
        public int CountByIp { get; set; } = 0;

        private string _quantity = "";
        public string Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity == value) return;
                _quantity = value;
                OnPropertyChanged();
            }
        }

        public string Key => $"{Name}||{Ip}";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? prop = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
