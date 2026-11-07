using System.Text;
using FileSearcher.Models;
using FileSearcher.Services;
using Xunit;

namespace FileSearcher.Tests
{
    /// <summary>
    /// CSV 导出回归测试：BOM、字段转义、表头
    /// （表头列随 F7 修复增加"行号"列时同步更新）
    /// </summary>
    public class CsvExportServiceTests : IDisposable
    {
        private readonly CsvExportService _service = new();
        private readonly string _dir = TestHelpers.CreateTempDirectory();

        [Fact]
        public void Export_WritesUtf8Bom()
        {
            string path = Path.Combine(_dir, "out.csv");
            _service.ExportToCsv(new List<SearchResult>
            {
                new() { FilePath = @"C:\a.txt", MatchIndex = 1, Context = "内容" }
            }, path);

            byte[] bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length >= 3 &&
                        bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                "CSV 应带 UTF-8 BOM，否则 Excel 直接打开会乱码");
        }

        [Fact]
        public void Export_EscapesCommaQuoteAndNewline()
        {
            string path = Path.Combine(_dir, "escape.csv");
            _service.ExportToCsv(new List<SearchResult>
            {
                new() { FilePath = @"C:\a.txt", MatchIndex = 1, Context = "a,b\"c\nd" }
            }, path);

            string content = File.ReadAllText(path);
            // 上下文字段含逗号/引号/换行 → 必须被引号包裹且内部引号翻倍
            Assert.Contains("\"a,b\"\"c\nd\"", content);
        }

        [Fact]
        public void Export_PlainField_NotQuoted()
        {
            string path = Path.Combine(_dir, "plain.csv");
            _service.ExportToCsv(new List<SearchResult>
            {
                new() { FilePath = @"C:\a.txt", MatchIndex = 2, Context = "无特殊字符" }
            }, path);

            string content = File.ReadAllText(path);
            Assert.Contains(@"C:\a.txt,2,无特殊字符", content);
        }

        [Fact]
        public void Export_HeaderRow_Correct()
        {
            string path = Path.Combine(_dir, "header.csv");
            _service.ExportToCsv(new List<SearchResult>(), path);

            string firstLine = File.ReadLines(path).First();
            Assert.Equal("文件路径,匹配序号,上下文内容", firstLine);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { /* 清理失败不影响测试结果 */ }
        }
    }
}
