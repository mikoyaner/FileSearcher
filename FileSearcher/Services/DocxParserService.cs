using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace FileSearcher.Services
{
    /// <summary>
    /// .docx 文件解析服务（使用 Open XML SDK）
    /// </summary>
    public class DocxParserService
    {
        /// <summary>
        /// 解析 .docx 文件，提取所有段落文本
        /// </summary>
        public string Parse(string filePath)
        {
            var sb = new StringBuilder();

            using (var document = WordprocessingDocument.Open(filePath, false))
            {
                var body = document.MainDocumentPart?.Document?.Body;
                if (body == null) return string.Empty;

                foreach (var paragraph in body.Elements<Paragraph>())
                {
                    var text = paragraph.InnerText;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine(text);
                    }
                }
            }

            return sb.ToString();
        }
    }
}
