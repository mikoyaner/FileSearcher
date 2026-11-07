using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;

namespace FileSearcher.Services
{
    /// <summary>
    /// 旧版 Office 格式解析服务（.doc / .xls / .ppt）
    /// </summary>
    public class LegacyParserService
    {
        /// <summary>
        /// 解析 .doc 文件
        /// NPOI 2.6.2 不包含 HWPF，使用二进制文本提取
        /// </summary>
        public string ParseDoc(string filePath)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var ms = new MemoryStream();
                fs.CopyTo(ms);
                byte[] bytes = ms.ToArray();

                // 从 OLE 二进制文档中提取可读文本
                return ExtractTextFromBinary(bytes);
            }
            catch
            {
                return "[无法解析 .doc 文件]";
            }
        }

        /// <summary>
        /// 解析 .xls 文件（使用 NPOI HSSF）
        /// </summary>
        public string ParseXls(string filePath)
        {
            var sb = new StringBuilder();

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                IWorkbook workbook = new HSSFWorkbook(fs);

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
        /// 解析 .ppt 文件
        /// NPOI 2.6.2 不包含 HSLF，使用二进制文本提取
        /// </summary>
        public string ParsePpt(string filePath)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var ms = new MemoryStream();
                fs.CopyTo(ms);
                byte[] bytes = ms.ToArray();

                return ExtractTextFromBinary(bytes);
            }
            catch
            {
                return "[无法解析 .ppt 文件，NPOI 对该格式支持有限]";
            }
        }

        /// <summary>
        /// 从 OLE 二进制文档中提取可读文本（UTF-16LE 和 UTF-8）
        /// </summary>
        private string ExtractTextFromBinary(byte[] bytes)
        {
            var sb = new StringBuilder();
            var textParts = new List<string>();

            // 尝试提取 UTF-16LE 编码的文本片段
            try
            {
                // 搜索连续的 Unicode 文本区域
                int i = 0;
                while (i < bytes.Length - 1)
                {
                    // 查找可能的 UTF-16LE 文本段
                    if (IsPrintableUnicode(bytes, i))
                    {
                        var segment = new StringBuilder();
                        int start = i;
                        while (i < bytes.Length - 1 && IsPrintableUnicode(bytes, i))
                        {
                            char c = (char)(bytes[i] | (bytes[i + 1] << 8));
                            if (c >= 0x4E00 && c <= 0x9FFF || c >= 0x3000 && c <= 0x303F ||
                                c >= 0xFF00 && c <= 0xFFEF || // 全角字符
                                (c >= 0x20 && c <= 0x7E)) // ASCII 可打印
                            {
                                segment.Append(c);
                            }
                            else if (c == '\r' || c == '\n')
                            {
                                segment.Append(' ');
                            }
                            i += 2;
                        }

                        // 只保留长度 >= 4 的文本段
                        if (segment.Length >= 4)
                        {
                            textParts.Add(segment.ToString().Trim());
                        }
                    }
                    else
                    {
                        i++;
                    }
                }
            }
            catch
            {
                // 忽略提取错误
            }

            if (textParts.Count > 0)
            {
                sb.AppendLine(string.Join("\r\n", textParts));
            }

            return sb.Length > 0 ? sb.ToString() : "[无法提取 .doc/.ppt 文件文本内容]";
        }

        private bool IsPrintableUnicode(byte[] bytes, int index)
        {
            if (index + 1 >= bytes.Length) return false;
            char c = (char)(bytes[index] | (bytes[index + 1] << 8));
            return (c >= 0x20 && c <= 0x7E) ||  // ASCII 可打印
                   (c >= 0x4E00 && c <= 0x9FFF) || // CJK 统一汉字
                   (c >= 0x3000 && c <= 0x303F) || // CJK 符号和标点
                   (c >= 0xFF00 && c <= 0xFFEF) || // 全角 ASCII 等
                   (c >= 0x00A0 && c <= 0x00FF);   // Latin-1 补充
        }

        /// <summary>
        /// 获取单元格文本
        /// F10 修复：与 XlsxParserService 相同 —— 单元格级异常降级，
        /// 避免单个坏单元格导致整个 .xls 文件被静默跳过
        /// </summary>
        private string GetCellText(ICell cell)
        {
            try
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
            catch
            {
                // 单格异常降级，不影响同文件其他单元格
                return cell.ToString() ?? "";
            }
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
