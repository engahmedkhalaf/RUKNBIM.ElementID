using OfficeOpenXml;
using System.Collections.Generic;
using System.IO;

namespace RUKNBIM.SmartSelect
{
    public class ExcelDataService : IDataService
    {
        public ExcelDataService()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public List<string> ImportIdsFromExcel(string filePath)
        {
            var ids = new List<string>();
            if (!File.Exists(filePath)) return ids;

            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                var worksheet = package.Workbook.Worksheets[0];
                if (worksheet == null) return ids;

                int rowCount = worksheet.Dimension?.Rows ?? 0;
                for (int row = 1; row <= rowCount; row++)
                {
                    var val = worksheet.Cells[row, 1].Text;
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        ids.Add(val.Trim());
                    }
                }
            }
            return ids;
        }

        public void ExportReportToExcel(string filePath, IEnumerable<string> foundIds, IEnumerable<string> missingIds)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Element Report");

                worksheet.Cells[1, 1].Value = "Found Elements";
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                int row = 2;
                foreach (var id in foundIds)
                {
                    worksheet.Cells[row++, 1].Value = id;
                }

                worksheet.Cells[1, 2].Value = "Missing Elements";
                worksheet.Cells[1, 2].Style.Font.Bold = true;
                row = 2;
                foreach (var id in missingIds)
                {
                    worksheet.Cells[row++, 2].Value = id;
                }

                worksheet.Cells.AutoFitColumns();

                package.SaveAs(new FileInfo(filePath));
            }
        }
    }
}
