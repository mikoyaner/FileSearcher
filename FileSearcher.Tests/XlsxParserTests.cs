using FileSearcher.Services;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Xunit;

namespace FileSearcher.Tests
{
    /// <summary>
    /// xlsx 解析回归测试：混合类型单元格的健壮性
    /// </summary>
    public class XlsxParserTests : IDisposable
    {
        private readonly SearchService _service = new();
        private readonly string _dir = TestHelpers.CreateTempDirectory();

        [Fact]
        public void Xlsx_DateFormulaAndTextCells_ParseWithoutError()
        {
            // F10 回归：日期/公式/文本混合单元格。
            // 修复前单元格级异常（如非法日期格式）会沿调用链上抛 → 整个文件被静默跳过；
            // 修复后单格降级，文件整体可解析、关键词可命中
            string path = Path.Combine(_dir, "sample.xlsx");
            CreateXlsx(path);

            var results = _service.SearchInFile(path, "秘密关键词");

            Assert.NotEmpty(results);
        }

        /// <summary>
        /// 构造包含日期、公式、文本三种类型单元格的 xlsx
        /// </summary>
        private static void CreateXlsx(string path)
        {
            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Sheet1");

            // 文本单元格（搜索目标）
            IRow row0 = sheet.CreateRow(0);
            row0.CreateCell(0).SetCellValue("文本单元格，包含秘密关键词");

            // 日期单元格（走 IsCellDateFormatted 分支）
            IRow row1 = sheet.CreateRow(1);
            var dateCell = row1.CreateCell(0);
            dateCell.SetCellValue(new DateTime(2026, 11, 8, 10, 30, 0));
            var dateStyle = workbook.CreateCellStyle();
            dateStyle.DataFormat = workbook.CreateDataFormat().GetFormat("yyyy-mm-dd hh:mm:ss");
            dateCell.CellStyle = dateStyle;

            // 公式单元格（走 CachedFormulaResultType 分支）
            row1.CreateCell(1).SetCellFormula("1+1");

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            workbook.Write(fs);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { /* 清理失败不影响测试结果 */ }
        }
    }
}
