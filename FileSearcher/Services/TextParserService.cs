using System.IO;
using FileSearcher.Helpers;

namespace FileSearcher.Services
{
    /// <summary>
    /// 纯文本文件解析服务（.txt, .csv, .log, .md, .json, .xml, .html, .htm）
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

        /// <summary>
        /// 解析纯文本文件，返回全文内容
        /// </summary>
        public string Parse(string filePath)
        {
            return EncodingHelper.ReadFileWithDetectedEncoding(filePath);
        }
    }
}
