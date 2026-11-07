using System.IO;

namespace FileSearcher.Services
{
    /// <summary>
    /// 纯文本文件类型判断服务（.txt, .csv, .log, .md, .json, .xml, .html, .htm）
    /// F9 重构后：文本内容的流式读取与匹配移入 SearchService.SearchInTextFile，
    /// 本服务仅负责文件类型判定
    /// </summary>
    public class TextParserService
    {
        private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".csv", ".log", ".md", ".json", ".xml", ".html", ".htm"
        };

        /// <summary>
        /// 判断是否为纯文本文件
        /// </summary>
        public bool CanParse(string filePath)
        {
            string ext = Path.GetExtension(filePath);
            return TextExtensions.Contains(ext);
        }
    }
}
