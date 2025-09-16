using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace WinUIOrderApp.Views.Dialogs
{
    public partial class InputDialog : Window
    {
        private static readonly string[] Fields = new[] { "name", "create", "ip", "inn" };

        /// <summary>
        /// Возвращает выбранное поле (name/create/ip/inn)
        /// </summary>
        public string ValueField => FieldCombo.SelectedItem as string ?? string.Empty;

        /// <summary>
        /// Возвращает введённое значение (из TextBox или из ComboBox)
        /// </summary>
        public string ValueText
        {
            get
            {
                var f = ValueField;
                if (f == "create" || f == "ip")
                    return (ValueCombo.SelectedItem as string) ?? (ValueCombo.Text ?? string.Empty);
                else
                    return ValueBox.Text ?? string.Empty;
            }
        }

        public InputDialog(string title = "Добавить фильтр", string initialField = "name", IEnumerable<string>? comboValues = null)
        {
            InitializeComponent();

            Title = title;

            // заполним список полей
            FieldCombo.ItemsSource = Fields;
            FieldCombo.SelectedItem = Fields.Contains(initialField) ? initialField : Fields[0];

            // если передали возможные значения для ValueCombo (create/ip), установим их
            if (comboValues != null)
            {
                ValueCombo.ItemsSource = comboValues;
                var first = comboValues.FirstOrDefault();
                if (first != null) ValueCombo.SelectedItem = first;
            }

            UpdateValueControls();
            UpdateOkEnabled();
        }

        // Обработчики событий — именно с такими именами ссылается XAML

        private void FieldCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateValueControls();
            UpdateOkEnabled();
        }

        private void ValueBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateOkEnabled();
        }

        private void ValueCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateOkEnabled();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // Вспомогательные методы

        private void UpdateValueControls()
        {
            var field = FieldCombo.SelectedItem as string ?? "name";
            if (field == "create" || field == "ip")
            {
                ValueCombo.Visibility = Visibility.Visible;
                ValueBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                ValueCombo.Visibility = Visibility.Collapsed;
                ValueBox.Visibility = Visibility.Visible;
            }
        }

        private void UpdateOkEnabled()
        {
            var field = FieldCombo.SelectedItem as string ?? "name";
            if (field == "create" || field == "ip")
            {
                var text = (ValueCombo.SelectedItem as string) ?? (ValueCombo.Text ?? string.Empty);
                OkButton.IsEnabled = !string.IsNullOrWhiteSpace(text);
            }
            else
            {
                OkButton.IsEnabled = !string.IsNullOrWhiteSpace(ValueBox.Text);
            }
        }

        /// <summary>
        /// Позволяет обновить список значений в Combo (например, подгрузка distinct create/ip из БД)
        /// </summary>
        public void SetComboValues(IEnumerable<string> values)
        {
            ValueCombo.ItemsSource = values;
            var first = values?.FirstOrDefault();
            if (first != null) ValueCombo.SelectedItem = first;
            UpdateOkEnabled();
        }
    }
}
