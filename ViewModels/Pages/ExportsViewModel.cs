using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using OfficeOpenXml;
using WinUIOrderApp.Models;

namespace WinUIOrderApp.ViewModels.Pages
{
    public class ExportsViewModel
    {
        private readonly string _connectionString = "Host=localhost;Database=marking_codes_db;Username=postgres;Password=0088;Port=5432";
        private readonly List<CisRow> _allRows = new();
        private readonly Dictionary<string, string> _qtyStore = new();

        public ObservableCollection<SummaryItem> SummaryItems { get; } = new();
        public bool IsDataLoaded
        {
            get; private set;
        }

        // Модели данных
        public class CisRow
        {
            public string Cis { get; set; } = string.Empty;
            public string Ki { get; set; } = string.Empty;
            public string Gtin { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Created { get; set; } = string.Empty;
            public string Ip { get; set; } = string.Empty;
            public string Inn { get; set; } = string.Empty;
        }

        public class CisItem
        {
            public string Cis { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        public class MarkCodesResult
        {
            public int ProcessedCount
            {
                get; set;
            }
            public int MarkedCount
            {
                get; set;
            }
            public int NotFoundCount
            {
                get; set;
            }
        }

        public class OrderProcessResult
        {
            public bool Success
            {
                get; set;
            }
            public string ErrorMessage { get; set; } = string.Empty;
            public int AssembledCodesCount
            {
                get; set;
            }
            public string OutputFilePath { get; set; } = string.Empty;
        }

        public class ApiResult
        {
            public bool Success
            {
                get; set;
            }
            public string Message { get; set; } = string.Empty;
            public string FilePath { get; set; } = string.Empty;
        }

        // Основные методы

        // Вспомогательные методы для обработки заказов
        private List<OrderItem> ReadOrderFromExcel(string filePath)
        {
            var orders = new List<OrderItem>();

            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                var worksheet = package.Workbook.Worksheets["Лист1"] ?? package.Workbook.Worksheets[0];

                if (worksheet == null)
                    throw new Exception("Не найден лист с данными в файле Excel");

                int rowCount = worksheet.Dimension?.Rows ?? 0;

                for (int row = 2; row <= rowCount; row++)
                {
                    var gtin = worksheet.Cells[row, 1]?.Text?.Trim() ?? "";
                    var productName = worksheet.Cells[row, 2]?.Text?.Trim() ?? "";
                    var quantityStr = worksheet.Cells[row, 3]?.Text?.Trim() ?? "";

                    if (string.IsNullOrEmpty(gtin) || string.IsNullOrEmpty(quantityStr))
                        continue;

                    gtin = new string(gtin.Where(char.IsDigit).ToArray()).PadLeft(14, '0');

                    if (gtin.Length != 14)
                        continue;

                    if (!int.TryParse(quantityStr, out int quantity) || quantity <= 0)
                        continue;

                    orders.Add(new OrderItem
                    {
                        Gtin = gtin,
                        ProductName = productName,
                        Quantity = quantity
                    });
                }
            }

            return orders;
        }


        private SaveResult SaveOrderResults(List<AssembledCode> assembledCodes, List<int> usedCodeIds)
        {
            try
            {
                var outputDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WinUIOrderApp", "Orders");
                Directory.CreateDirectory(outputDir);

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var excelFilePath = Path.Combine(outputDir, $"order_{timestamp}.xlsx");
                var txtFilePath = Path.Combine(outputDir, $"order_codes_{timestamp}.txt");

                SaveToExcel(assembledCodes, excelFilePath);
                SaveToTextFile(assembledCodes, txtFilePath);

                return new SaveResult
                {
                    Success = true,
                    OutputFilePath = excelFilePath
                };
            }
            catch (Exception ex)
            {
                return new SaveResult
                {
                    Success = false,
                    ErrorMessage = $"Ошибка сохранения результатов: {ex.Message}"
                };
            }
        }

        private void SaveToExcel(List<AssembledCode> codes, string filePath)
        {
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("КМ.общ. (для заказов)");

            worksheet.Cells[1, 1].Value = "КМ";
            worksheet.Cells[1, 2].Value = "КМ";
            worksheet.Cells[1, 3].Value = "ГТИН";
            worksheet.Cells[1, 4].Value = "ИМЯ";
            worksheet.Cells[1, 5].Value = "МАРКА";
            worksheet.Cells[1, 6].Value = "ИП";
            worksheet.Cells[1, 7].Value = "ИНН ИП";

            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                worksheet.Cells[i + 2, 1].Value = code.FullCode;
                worksheet.Cells[i + 2, 2].Value = code.FullCode;
                worksheet.Cells[i + 2, 3].Value = code.Gtin;
                worksheet.Cells[i + 2, 4].Value = code.ProductName;
                worksheet.Cells[i + 2, 5].Value = code.Brand;
                worksheet.Cells[i + 2, 6].Value = code.OwnerName;
                worksheet.Cells[i + 2, 7].Value = code.Inn;
            }

            worksheet.Cells[1, 1, 1, 7].Style.Font.Bold = true;
            worksheet.Cells[1, 1, 1, 7].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
            worksheet.Cells.AutoFitColumns();

            package.SaveAs(new FileInfo(filePath));
        }

        private void SaveToTextFile(List<AssembledCode> codes, string filePath)
        {
            using var writer = new StreamWriter(filePath);
            foreach (var code in codes)
            {
                writer.WriteLine(code.FullCode);
            }
        }


        // Вспомогательные классы
        public class OrderItem
        {
            public string Gtin { get; set; } = string.Empty;
            public string ProductName { get; set; } = string.Empty;
            public int Quantity
            {
                get; set;
            }
        }

        public class CodeInfo
        {
            public int Id
            {
                get; set;
            }
            public string FullCode { get; set; } = string.Empty;
            public string Brand { get; set; } = string.Empty;
            public string ProductName { get; set; } = string.Empty;
            public string OwnerName { get; set; } = string.Empty;
            public string Inn { get; set; } = string.Empty;
            public int GroupNumber
            {
                get; set;
            }
        }

        public class AssembledCode
        {
            public string FullCode { get; set; } = string.Empty;
            public string Gtin { get; set; } = string.Empty;
            public string ProductName { get; set; } = string.Empty;
            public string Brand { get; set; } = string.Empty;
            public string OwnerName { get; set; } = string.Empty;
            public string Inn { get; set; } = string.Empty;
            public int GroupNumber
            {
                get; set;
            }
        }

        public class OrderAssemblyResult
        {
            public bool Success
            {
                get; set;
            }
            public string ErrorMessage { get; set; } = string.Empty;
            public List<AssembledCode> AssembledCodes { get; set; } = new List<AssembledCode>();
            public List<int> UsedCodeIds { get; set; } = new List<int>();
        }

        public class SaveResult
        {
            public bool Success
            {
                get; set;
            }
            public string OutputFilePath { get; set; } = string.Empty;
            public string ErrorMessage { get; set; } = string.Empty;
        }

        // Заглушки для API методов (они реализованы в DataPage)
        public async Task<ApiResult> RequestKmAsync(string token)
        {
            return await Task.FromResult(new ApiResult { Success = false, Message = "Метод не реализован" });
        }

        public async Task<ApiResult> RequestUpdAsync(string token, string mode, string status, string certificateOwner)
        {
            return await Task.FromResult(new ApiResult { Success = false, Message = "Метод не реализован" });
        }

        public async Task<ApiResult> RequestEdoAsync(string token, string mode, string statusText)
        {
            return await Task.FromResult(new ApiResult { Success = false, Message = "Метод не реализован" });
        }
    }
}