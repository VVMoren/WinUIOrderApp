using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;

namespace markapp.Views.Pages
{
    /// <summary>Взаимодействие для SearchPage.xaml</summary>
    public partial class SearchPage : Page
    {
        private readonly SearchPageViewModel _vm;

        public SearchPage()
        {
            InitializeComponent();
            _vm = new SearchPageViewModel(ApplyColumns);
            DataContext = _vm;
        }

        /// <summary>
        /// Полная перестройка столбцов грида (после выбора режима).
        /// Первые два (☑ и №) остаются, дальнейшие — динамика.
        /// </summary>
        private void ApplyColumns(IReadOnlyList<ColDef> cols)
        {
            // оставляем 2 первых
            while (DataGrid.Columns.Count > 2)
                DataGrid.Columns.RemoveAt(2);

            foreach (var c in cols)
            {
                var col = new DataGridTextColumn
                {
                    Header = c.Header, // то, что видим в таблице
                    Binding = new Binding($"Cells[{c.Key}]") { TargetNullValue = "" },
                    Width = c.WidthStar ? new DataGridLength(1, DataGridLengthUnitType.Star) : DataGridLength.Auto
                };
                DataGrid.Columns.Add(col);
            }
        }
    }
}
