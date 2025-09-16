using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace markapp.Views.Windows
{
    public partial class DellDiapason : Window
    {
        public string StartIndex => StartIndexTextBox.Text;
        public string EndIndex => EndIndexTextBox.Text;

        public DellDiapason()
        {
            InitializeComponent();
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            // Закрываем окно и передаем результат
            this.DialogResult = true;
            this.Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            // Закрываем окно без результата
            this.DialogResult = false;
            this.Close();
        }
    }

}
