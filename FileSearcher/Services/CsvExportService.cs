using System.IO;
using System.Text;
using FileSearcher.Models;

namespace FileSearcher.Services
{
    /// <summary>
    /// CSV 导出服务
    /// </summary>
    public class CsvExportService
    {
        /// <summary>
        /// 导出搜索结果为 CSV 文件
        /// 编码：UTF-8 with BOM（确保 Excel 打开不乱码）
        /// </summary>
        public void ExportToCsv(List<SearchResult> results, string outputPath)
        {
            // 使用 UTF-8 with BOM
            var utf8Bom = new UTF8Encoding(true);

            using (var writer = new StreamWriter(outputPath, false, utf8Bom))
            {
                // F7：表头新增"行号"列 —— SearchResult.LineNumber 此前已计算但未导出，信息丢失
                writer.WriteLine("文件路径,匹配序号,行号,上下文内容");

                // F7：导出前按 文件路径 + 匹配序号 排序 —— 并行搜索按完成顺序入队，
                // 直接导出行序非确定（同一文件的匹配会被其他文件的行打断）
                foreach (var result in results
                    .OrderBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(r => r.MatchIndex))
                {
                    string filePath = EscapeCsvField(result.FilePath);
                    string context = EscapeCsvField(result.Context);
                    writer.WriteLine($"{filePath},{result.MatchIndex},{result.LineNumber},{context}");
                }
            }
        }

        /// <summary>
        /// CSV 字段转义（处理逗号、引号、换行符）
        /// </summary>
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "\"\"";

            // 如果包含逗号、引号或换行符，需要用引号包裹
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                // 引号转义：双引号替换为两个双引号
                field = field.Replace("\"", "\"\"");
                return $"\"{field}\"";
            }

            return field;
        }
    }
}
