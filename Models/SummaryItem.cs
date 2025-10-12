using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinUIOrderApp.Models
{
    public class SummaryItem : INotifyPropertyChanged
    {
        private string _quantity = string.Empty;

        public string Name { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        public string Creator { get; set; } = string.Empty;
        public int TotalByName
        {
            get; set;
        }
        public int CountByIp
        {
            get; set;
        }

        public string Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity != value)
                {
                    _quantity = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Key => $"{Name}||{Ip}";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}