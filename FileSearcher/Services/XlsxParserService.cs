using System.IO;
using System.Text;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace FileSearcher.Services
{
    /// <summary>
    /// .xlsx 文件解析服务（使用 NPOI）
    /// </summary>
    public class XlsxParserService
    {
        /// <summary>
        /// 解析 .xlsx 文件，读取所有 Sheet 的所有单元格文本
        /// </summary>
        public string Parse(string filePath)
        {
            var sb = new StringBuilder();

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                IWorkbook workbook = new XSSFWorkbook(fs);

                for (int i = 0; i < workbook.NumberOfSheets; i++)
                {
                    ISheet sheet = workbook.GetSheetAt(i);
                    sb.AppendLine($"[Sheet: {sheet.SheetName}]");

                    for (int rowIdx = sheet.FirstRowNum; rowIdx <= sheet.LastRowNum; rowIdx++)
                    {
                        IRow? row = sheet.GetRow(rowIdx);
                        if (row == null) continue;

                        var rowTexts = new List<string>();
                        for (int colIdx = row.FirstCellNum; colIdx < row.LastCellNum; colIdx++)
                        {
                            ICell? cell = row.GetCell(colIdx);
                            if (cell != null)
                            {
                                rowTexts.Add(GetCellText(cell));
                            }
                        }

                        if (rowTexts.Count > 0)
                        {
                            sb.AppendLine(string.Join("\t", rowTexts));
                        }
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 获取单元格文本
        /// </summary>
        private string GetCellText(ICell cell)
        {
            return cell.CellType switch
            {
                CellType.Numeric => DateUtil.IsCellDateFormatted(cell)
                    ? cell.DateCellValue.ToString("yyyy-MM-dd HH:mm:ss")
                    : cell.NumericCellValue.ToString(),
                CellType.String => cell.StringCellValue ?? "",
                CellType.Boolean => cell.BooleanCellValue.ToString(),
                CellType.Formula => GetFormulaCellText(cell),
                _ => cell.ToString() ?? ""
            };
        }

        private string GetFormulaCellText(ICell cell)
        {
            try
            {
                return cell.CachedFormulaResultType switch
                {
                    CellType.Numeric => cell.NumericCellValue.ToString(),
                    CellType.String => cell.StringCellValue ?? "",
                    CellType.Boolean => cell.BooleanCellValue.ToString(),
                    _ => cell.ToString() ?? ""
                };
            }
            catch
            {
                return cell.ToString() ?? "";
            }
        }
    }
}
